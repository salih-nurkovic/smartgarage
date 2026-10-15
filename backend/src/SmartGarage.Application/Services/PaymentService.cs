using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Payments;
using SmartGarage.Application.Options;
using SmartGarage.Contracts.Events;
using SmartGarage.Contracts.Routing;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IPaymentService
{
    Task<PaymentIntentResponse> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct);

    Task<PagedResult<PaymentResponse>> GetAsync(PaymentSearchRequest search, CancellationToken ct);

    Task<PaymentSummaryResponse> GetSummaryAsync(CancellationToken ct);

    /// <summary>
    /// The payment, checked against Stripe first if it is still open. This is the server-side
    /// verification the app polls after the payment sheet closes.
    /// </summary>
    Task<PaymentResponse> GetByIdAsync(int id, CancellationToken ct);

    Task<PaymentResponse> RefundAsync(int id, RefundRequest request, CancellationToken ct);

    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct);

    /// <summary>Settles payments whose webhook never arrived. Run by the Worker on a schedule.</summary>
    Task<int> ReconcileStaleAsync(CancellationToken ct);
}

/// <summary>
/// Payments through Stripe and refunds of them.
/// <para>
/// Two rules from the instructions shape all of it. The amount is always worked out here, from
/// the price snapshots on the appointment - the client sends only which appointment to pay.
/// And a payment is only ever marked successful because Stripe said so, either through a signed
/// webhook or because the server asked Stripe itself; never because the app reported it.
/// </para>
/// <para>
/// Every route into a payment's state - webhook, status check, scheduled reconciliation - goes
/// through <see cref="ApplyAsync"/>, which only acts on an actual change. That is what makes
/// confirmation idempotent: Stripe can send the same event ten times and the customer is still
/// charged, notified and marked paid exactly once.
/// </para>
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const decimal MinorUnitsPerUnit = 100m;
    private const int StaleAfterMinutes = 30;
    private const int ReconcileBatchSize = 100;
    private const string DisplayCurrency = "EUR";

    private static readonly PaymentStatus[] Open =
        [PaymentStatus.Created, PaymentStatus.RequiresAction, PaymentStatus.Processing];

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IStripeGateway _stripe;
    private readonly IEventPublisher _events;
    private readonly ILogger<PaymentService> _logger;
    private readonly string _stripeCurrency;

    public PaymentService(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IStripeGateway stripe,
        IEventPublisher events,
        IOptions<PaymentOptions> options,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _stripe = stripe;
        _events = events;
        _logger = logger;
        _stripeCurrency = options.Value.StripeCurrency;
    }

    private bool IsStaff =>
        _currentUser.IsInRole(Roles.Administrator) || _currentUser.IsInRole(Roles.Employee);

    // A customer's queries contain only the payments they made.
    private IQueryable<Payment> Scope
    {
        get
        {
            if (IsStaff || _currentUser.IsInRole(Roles.System))
            {
                return _db.Set<Payment>();
            }

            var userId = _currentUser.RequireUserId();
            return _db.Set<Payment>().Where(p => p.PayerId == userId);
        }
    }

    public async Task<PaymentIntentResponse> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var appointment = await _db.Set<Appointment>()
                .Include(a => a.Items)
                .SingleOrDefaultAsync(a => a.Id == request.AppointmentId && a.CustomerId == userId, ct)
            ?? throw new ValidationException("appointmentId", "Izabrana rezervacija ne postoji.");

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new BusinessRuleException("Plaćanje je moguće tek nakon što servis bude završen.");
        }

        // The price is the sum of what was agreed when the appointment was booked.
        var amount = appointment.Items.Sum(i => i.LineTotal);

        var open = await _db.Set<Payment>()
            .Where(p => p.AppointmentId == appointment.Id && Open.Contains(p.Status))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (open is not null)
        {
            // Asking again does not start a second payment: the one already open is checked
            // with Stripe and, if it can still be paid, handed back.
            var current = await _stripe.GetPaymentIntentAsync(open.StripePaymentIntentId, ct);
            await ApplyAsync(open, current, ct);

            if (open.Status != PaymentStatus.Succeeded && open.Status != PaymentStatus.Failed
                && open.Status != PaymentStatus.Cancelled)
            {
                return ToIntentResponse(open, current.ClientSecret);
            }
        }

        if (appointment.PaymentStatus is not (AppointmentPaymentStatus.Unpaid or AppointmentPaymentStatus.PendingPayment))
        {
            throw new ConflictException("Ova rezervacija je već plaćena.");
        }

        // Derived from the appointment and the attempt number, so two simultaneous requests
        // send Stripe the same key and get the same PaymentIntent back rather than two.
        var attempt = await _db.Set<Payment>().CountAsync(p => p.AppointmentId == appointment.Id, ct) + 1;
        var idempotencyKey = $"appt-{appointment.Id}-attempt-{attempt}";
        var paymentNumber = await NextNumberAsync(ct);

        var intent = await _stripe.CreatePaymentIntentAsync(
            ToMinor(amount),
            _stripeCurrency,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["appointmentNumber"] = appointment.AppointmentNumber,
                ["paymentNumber"] = paymentNumber,
            },
            idempotencyKey,
            ct);

        var payment = new Payment
        {
            PaymentNumber = paymentNumber,
            AppointmentId = appointment.Id,
            PayerId = userId,
            Amount = amount,
            Currency = DisplayCurrency,
            StripeCurrency = _stripeCurrency.ToUpperInvariant(),
            StripePaymentIntentId = intent.Id,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = _clock.UtcNow,
        };

        _db.Set<Payment>().Add(payment);
        appointment.PaymentStatus = AppointmentPaymentStatus.PendingPayment;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The other request of a simultaneous pair already stored this same intent.
            _db.DiscardChanges();

            var stored = await _db.Set<Payment>().SingleAsync(p => p.StripePaymentIntentId == intent.Id, ct);
            return ToIntentResponse(stored, intent.ClientSecret);
        }

        return ToIntentResponse(payment, intent.ClientSecret);
    }

    public async Task<PagedResult<PaymentResponse>> GetAsync(PaymentSearchRequest search, CancellationToken ct)
    {
        var query = Scope.AsNoTracking();

        if (search.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (search.From is { } from)
        {
            query = query.Where(p => p.CreatedAtUtc >= from);
        }

        if (search.To is { } to)
        {
            query = query.Where(p => p.CreatedAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();

            query = query.Where(p =>
                p.PaymentNumber.Contains(term)
                || p.Appointment.AppointmentNumber.Contains(term)
                || p.Payer.FirstName.Contains(term)
                || p.Payer.LastName.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await Project(query
                .OrderByDescending(p => p.CreatedAtUtc)
                .ThenByDescending(p => p.Id)
                .Skip(search.Skip)
                .Take(search.ClampedPageSize))
            .ToListAsync(ct);

        return new PagedResult<PaymentResponse>(items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<PaymentSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var byStatus = await Scope
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Charged = g.Sum(p => p.AmountChargedStripe) })
            .ToListAsync(ct);

        var refunded = await Scope
            .SelectMany(p => p.Refunds)
            .Where(r => r.Status == RefundStatus.Succeeded)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0;

        return new PaymentSummaryResponse
        {
            NetRevenue = byStatus.Where(s => s.Status == PaymentStatus.Succeeded).Sum(s => s.Charged) - refunded,
            Succeeded = byStatus.Where(s => s.Status == PaymentStatus.Succeeded).Sum(s => s.Count),
            InProgress = byStatus.Where(s => Open.Contains(s.Status)).Sum(s => s.Count),
            Failed = byStatus.Where(s => s.Status is PaymentStatus.Failed or PaymentStatus.Cancelled).Sum(s => s.Count),
        };
    }

    public async Task<PaymentResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var payment = await Scope.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw NotFound();

        if (Open.Contains(payment.Status))
        {
            var intent = await _stripe.GetPaymentIntentAsync(payment.StripePaymentIntentId, ct);
            await ApplyAsync(payment, intent, ct);
        }

        return await Project(_db.Set<Payment>().AsNoTracking().Where(p => p.Id == id)).SingleAsync(ct);
    }

    public async Task<PaymentResponse> RefundAsync(int id, RefundRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var payment = await _db.Set<Payment>()
                .Include(p => p.Appointment)
                .Include(p => p.Refunds)
                .SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFound();

        if (payment.Status != PaymentStatus.Succeeded)
        {
            throw new ConflictException("Povrat je moguć samo za uspješno izvršeno plaćanje.");
        }

        // Limited by what Stripe actually took, not by the listed price (instructions, 7.1).
        var alreadyRefunded = payment.Refunds
            .Where(r => r.Status is RefundStatus.Pending or RefundStatus.Succeeded)
            .Sum(r => r.Amount);
        var refundable = payment.AmountChargedStripe - alreadyRefunded;
        var amount = request.Amount ?? refundable;

        if (refundable <= 0)
        {
            throw new ConflictException("Za ovo plaćanje je već izvršen puni povrat.");
        }

        if (amount <= 0 || amount > refundable)
        {
            throw new ValidationException(
                "amount",
                $"Iznos povrata mora biti veći od 0 i najviše {refundable.ToString("0.00", CultureInfo.InvariantCulture)} {payment.Currency}.");
        }

        var refund = await _stripe.CreateRefundAsync(
            payment.StripePaymentIntentId,
            ToMinor(amount),
            $"refund-{payment.Id}-{payment.Refunds.Count + 1}",
            ct);

        var now = _clock.UtcNow;
        var status = ToRefundStatus(refund.Status);

        payment.Refunds.Add(new Refund
        {
            Amount = amount,
            Reason = request.Reason.Trim(),
            Status = status,
            StripeRefundId = refund.Id,
            RequestedByUserId = userId,
            RequestedAtUtc = now,
            CompletedAtUtc = status == RefundStatus.Succeeded ? now : null,
        });

        payment.Appointment.PaymentStatus = amount == refundable
            ? AppointmentPaymentStatus.Refunded
            : AppointmentPaymentStatus.PartiallyRefunded;

        await _db.SaveChangesAsync(ct);

        await _events.PublishAsync(
            new RefundIssuedEvent
            {
                PaymentId = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                AppointmentId = payment.AppointmentId,
                PayerId = payment.PayerId,
                Amount = amount,
                Currency = payment.Currency,
                Reason = request.Reason.Trim(),
                CorrelationId = _currentUser.CorrelationId,
            },
            MessageRouting.RoutingKeys.RefundIssued,
            ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct)
    {
        StripeWebhookEvent stripeEvent;

        try
        {
            stripeEvent = _stripe.ParseWebhook(payload, signatureHeader);
        }
#pragma warning disable CA1031 // Any reason the signature does not verify ends the same way: refused.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogWarning(exception, "Rejected a Stripe webhook whose signature did not verify.");
            throw new ValidationException("signature", "Potpis zahtjeva nije ispravan.");
        }

        var paymentEvent = new PaymentEvent
        {
            StripeEventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            ReceivedAtUtc = _clock.UtcNow,
            RawPayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
        };

        _db.Set<PaymentEvent>().Add(paymentEvent);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on the event id: this event was received before. Stripe retries
            // deliveries, and the answer to a retry is a quiet success with nothing done twice.
            _db.DiscardChanges();
            _logger.LogInformation("Stripe event {EventId} was already processed.", stripeEvent.Id);
            return;
        }

        var payment = stripeEvent.PaymentIntentId is null
            ? null
            : await _db.Set<Payment>().SingleOrDefaultAsync(p => p.StripePaymentIntentId == stripeEvent.PaymentIntentId, ct);

        if (payment is null)
        {
            paymentEvent.ProcessingResult = "No payment in this system matches the event.";
        }
        else if (stripeEvent.RefundId is not null)
        {
            var refund = await _db.Set<Refund>().SingleOrDefaultAsync(r => r.StripeRefundId == stripeEvent.RefundId, ct);

            if (refund is not null && stripeEvent.RefundStatus is not null)
            {
                refund.Status = ToRefundStatus(stripeEvent.RefundStatus);
                refund.CompletedAtUtc ??= refund.Status == RefundStatus.Succeeded ? _clock.UtcNow : null;
            }

            paymentEvent.PaymentId = payment.Id;
            paymentEvent.ProcessingResult = "Refund updated.";
        }
        else
        {
            // The event says something changed; what it changed to is asked of Stripe directly,
            // so an event that arrives late or out of order cannot move a payment backwards.
            var intent = await _stripe.GetPaymentIntentAsync(payment.StripePaymentIntentId, ct);
            await ApplyAsync(payment, intent, ct);

            paymentEvent.PaymentId = payment.Id;
            paymentEvent.ProcessingResult = $"Payment is {payment.Status}.";
        }

        paymentEvent.ProcessedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ReconcileStaleAsync(CancellationToken ct)
    {
        if (!_currentUser.IsInRole(Roles.System))
        {
            throw new ForbiddenException("Ovu radnju izvršava samo sistem.");
        }

        var cutoff = _clock.UtcNow.AddMinutes(-StaleAfterMinutes);

        var stale = await _db.Set<Payment>()
            .Where(p => Open.Contains(p.Status) && p.CreatedAtUtc < cutoff)
            .OrderBy(p => p.Id)
            .Take(ReconcileBatchSize)
            .ToListAsync(ct);

        foreach (var payment in stale)
        {
            var intent = await _stripe.GetPaymentIntentAsync(payment.StripePaymentIntentId, ct);
            await ApplyAsync(payment, intent, ct);
        }

        return stale.Count;
    }

    /// <summary>
    /// Brings a payment in line with what Stripe reports. Does nothing unless the status
    /// actually changes, and a successful payment never changes again.
    /// </summary>
    private async Task ApplyAsync(Payment payment, StripeIntent intent, CancellationToken ct)
    {
        var target = intent.Status switch
        {
            "succeeded" => PaymentStatus.Succeeded,
            "processing" => PaymentStatus.Processing,
            "requires_action" or "requires_confirmation" => PaymentStatus.RequiresAction,
            "canceled" => PaymentStatus.Cancelled,
            "requires_payment_method" when intent.FailureMessage is not null => PaymentStatus.Failed,
            _ => PaymentStatus.Created,
        };

        if (target == payment.Status || payment.Status == PaymentStatus.Succeeded)
        {
            return;
        }

        var appointment = await _db.Set<Appointment>().SingleAsync(a => a.Id == payment.AppointmentId, ct);

        payment.Status = target;

        switch (target)
        {
            case PaymentStatus.Succeeded:
                payment.PaidAtUtc = _clock.UtcNow;
                payment.StripeChargeId = intent.LatestChargeId;
                payment.AmountChargedStripe = intent.AmountReceivedMinor / MinorUnitsPerUnit;
                payment.FailureReason = null;
                appointment.PaymentStatus = AppointmentPaymentStatus.Paid;
                break;

            case PaymentStatus.Failed or PaymentStatus.Cancelled:
                payment.FailureReason = intent.FailureMessage ?? "Plaćanje je otkazano.";
                appointment.PaymentStatus = AppointmentPaymentStatus.Unpaid;
                break;

            default:
                break;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Payment {PaymentNumber} is now {Status} according to Stripe.", payment.PaymentNumber, target);

        if (target == PaymentStatus.Succeeded)
        {
            await _events.PublishAsync(
                new PaymentSucceededEvent
                {
                    PaymentId = payment.Id,
                    PaymentNumber = payment.PaymentNumber,
                    AppointmentId = appointment.Id,
                    AppointmentNumber = appointment.AppointmentNumber,
                    PayerId = payment.PayerId,
                    Amount = payment.AmountChargedStripe,
                    Currency = payment.Currency,
                    CorrelationId = _currentUser.CorrelationId,
                },
                MessageRouting.RoutingKeys.PaymentSucceeded,
                ct);
        }
        else if (target is PaymentStatus.Failed)
        {
            await _events.PublishAsync(
                new PaymentFailedEvent
                {
                    PaymentId = payment.Id,
                    AppointmentId = appointment.Id,
                    AppointmentNumber = appointment.AppointmentNumber,
                    PayerId = payment.PayerId,
                    Reason = payment.FailureReason,
                    CorrelationId = _currentUser.CorrelationId,
                },
                MessageRouting.RoutingKeys.PaymentFailed,
                ct);
        }
    }

    private PaymentIntentResponse ToIntentResponse(Payment payment, string clientSecret) => new()
    {
        PaymentId = payment.Id,
        PaymentNumber = payment.PaymentNumber,
        ClientSecret = clientSecret,
        PublishableKey = _stripe.PublishableKey,
        Amount = payment.Amount,
        Currency = payment.Currency,
    };

    private static IQueryable<PaymentResponse> Project(IQueryable<Payment> query) =>
        query.Select(p => new PaymentResponse
        {
            Id = p.Id,
            PaymentNumber = p.PaymentNumber,
            AppointmentId = p.AppointmentId,
            AppointmentNumber = p.Appointment.AppointmentNumber,
            PayerFullName = p.Payer.FirstName + " " + p.Payer.LastName,
            ServiceNames = p.Appointment.Items.Select(i => i.Service.Name).ToList(),
            Amount = p.Amount,
            Currency = p.Currency,
            Status = p.Status,
            CreatedAtUtc = p.CreatedAtUtc,
            PaidAtUtc = p.PaidAtUtc,
            FailureReason = p.FailureReason,
            AmountCharged = p.AmountChargedStripe,
            RefundedAmount = p.Refunds
                .Where(r => r.Status == RefundStatus.Pending || r.Status == RefundStatus.Succeeded)
                .Sum(r => (decimal?)r.Amount) ?? 0,
            Refunds = p.Refunds
                .OrderBy(r => r.RequestedAtUtc)
                .Select(r => new RefundResponse
                {
                    Amount = r.Amount,
                    Reason = r.Reason,
                    Status = r.Status,
                    RequestedAtUtc = r.RequestedAtUtc,
                    CompletedAtUtc = r.CompletedAtUtc,
                })
                .ToList(),
        });

    // Unique index on the number backs this up if two payments are ever started at once.
    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var prefix = $"PAY-{_clock.UtcNow.Year}-";

        var last = await _db.Set<Payment>()
            .Where(p => p.PaymentNumber.StartsWith(prefix))
            .MaxAsync(p => (string?)p.PaymentNumber, ct);

        var sequence = last is null
            ? 1
            : int.Parse(last.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture) + 1;

        return $"{prefix}{sequence:D6}";
    }

    private static long ToMinor(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * MinorUnitsPerUnit, 0, MidpointRounding.AwayFromZero));

    private static RefundStatus ToRefundStatus(string stripeStatus) => stripeStatus switch
    {
        "succeeded" => RefundStatus.Succeeded,
        "failed" => RefundStatus.Failed,
        "canceled" => RefundStatus.Cancelled,
        _ => RefundStatus.Pending,
    };

    private static NotFoundException NotFound() => new("Plaćanje nije pronađeno.");
}
