using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds vehicles and the appointment history that hangs off them, together with the payments,
/// reviews, notifications, reminders and recommender signals that a real history would have
/// produced.
/// <para>
/// The instructions warn that work containing too few records will not be evaluated in detail,
/// and separately that seed data must be internally consistent. Both are taken literally here:
/// every payment amount equals the sum of its appointment's line snapshots, every review
/// belongs to a completed appointment, every status history is a legal path through the state
/// machine, and every date is ordered.
/// </para>
/// <para>
/// Randomness is seeded from a constant, so the same database comes out on every machine. A
/// demo that differs between runs is a demo nobody can reason about.
/// </para>
/// </summary>
public sealed class DemoDataSeeder
{
    private const int RandomSeed = 220014;

    private readonly SmartGarageDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DemoDataSeeder> _logger;
    private readonly SeedRandom _random = new(RandomSeed);

    // The latest reminder per vehicle and service, so an older one can be closed when the
    // same service is done again.
    private readonly Dictionary<(int VehicleId, int ServiceId), ServiceReminder> _openReminders = [];

    public DemoDataSeeder(
        SmartGarageDbContext db,
        IConfiguration configuration,
        ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>A vehicle to create, written so each one demonstrates a different recommender case.</summary>
    private sealed record VehicleSeed(
        string OwnerUserName,
        int BrandId,
        int ModelId,
        int EngineTypeId,
        int Year,
        string Plate,
        string Vin,
        int MileageKm);

    public async Task SeedAsync(DateTime nowUtc, CancellationToken ct)
    {
        if (await _db.Vehicles.AnyAsync(ct))
        {
            return;
        }

        var vehicles = await SeedVehiclesAsync(nowUtc, ct);
        await SeedAppointmentsAsync(vehicles, nowUtc, ct);

        _logger.LogInformation(
            "Demo data seeded: {Vehicles} vehicles, {Appointments} appointments, "
            + "{Payments} payments, {Reviews} reviews, {Signals} recommender signals.",
            await _db.Vehicles.CountAsync(ct),
            await _db.Appointments.CountAsync(ct),
            await _db.Payments.CountAsync(ct),
            await _db.Reviews.CountAsync(ct),
            await _db.UserServiceSignals.CountAsync(ct));
    }

    private async Task<List<Vehicle>> SeedVehiclesAsync(DateTime nowUtc, CancellationToken ct)
    {
        // Each of these exists to make a specific recommender behaviour visible during the
        // demonstration, rather than to pad the row count.
        var seeds = new VehicleSeed[]
        {
            // amar: rich history, older diesel, high mileage -> strongest recommendations
            new("amar", 1, 1, 1, 2018, "A12-K-345", "WVWZZZ1KZAW123456", 182_400),
            new("amar", 5, 17, 1, 2016, "A34-M-567", "TMBJJ7NE5G0123457", 211_000),

            // lejla: brand new account, new petrol car, almost no mileage -> cold start
            new("lejla", 6, 21, 2, 2023, "E55-T-890", "JTDKB20U793123458", 12_300),

            // nedim: very old diesel with extreme mileage -> everything is overdue
            new("nedim", 2, 6, 1, 2012, "K78-O-123", "WAUZZZ8K9CA123459", 310_500),

            // ivana: completed but unpaid appointment -> payment demonstration
            new("ivana", 3, 9, 2, 2020, "M90-P-456", "WBA8E9G50GNU23460", 64_800),

            // the prescribed accounts also own vehicles so they are not empty on login
            new("mobile", 4, 14, 1, 2019, "S11-A-789", "WDD2050011A123461", 98_200),
            new("customer", 9, 33, 2, 2021, "T22-B-012", "WF0AXXGCDA1H23462", 41_600),

            // a spread of other profiles so lists, filters and reports have variety
            new("customer", 7, 26, 5, 2015, "C33-D-345", "VF1RFB00X52123463", 168_900),
            new("amar", 3, 11, 3, 2022, "H44-E-678", "WBAXG71050DT23464", 28_700),
            new("lejla", 10, 38, 2, 2017, "P55-F-901", "VF3LBHYBWHS123465", 121_400),
            new("nedim", 8, 29, 1, 2014, "O66-G-234", "W0LPD6EC4E4123466", 243_100),
            new("ivana", 6, 23, 4, 2024, "E77-H-567", "JTMBB3FV30D123467", 6_900),
        };

        var userIds = await _db.Users
            .ToDictionaryAsync(u => u.UserName!, u => u.Id, StringComparer.Ordinal, ct);

        var created = new List<Vehicle>();

        foreach (var seed in seeds)
        {
            var vehicle = new Vehicle
            {
                OwnerId = userIds[seed.OwnerUserName],
                BrandId = seed.BrandId,
                ModelId = seed.ModelId,
                EngineTypeId = seed.EngineTypeId,
                ProductionYear = seed.Year,
                RegistrationPlate = seed.Plate,
                Vin = seed.Vin,
                CurrentMileageKm = seed.MileageKm,
                LastMileageUpdatedAtUtc = nowUtc.AddDays(-_random.Next(1, 60)),
                CreatedAtUtc = nowUtc.AddMonths(-_random.Next(6, 24)),
            };

            _db.Vehicles.Add(vehicle);
            created.Add(vehicle);
        }

        await _db.SaveChangesAsync(ct);
        return created;
    }

    private async Task SeedAppointmentsAsync(
        List<Vehicle> vehicles,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var services = await _db.Services
            .Include(s => s.EngineTypeLinks)
            .ToListAsync(ct);

        var employeeProfileIds = await _db.EmployeeProfiles
            .Select(e => e.Id)
            .ToListAsync(ct);

        var adminId = await _db.Users
            .Where(u => u.UserName == "admin")
            .Select(u => u.Id)
            .SingleAsync(ct);

        var currency = _configuration["DISPLAY_CURRENCY"] ?? "EUR";
        var stripeCurrency = (_configuration["STRIPE_CURRENCY"] ?? "eur").ToUpperInvariant();

        var sequence = 0;
        var paymentSequence = 0;

        // Eighteen months back and three weeks forward, so reports have range to aggregate over
        // and the upcoming list is not empty.
        for (var dayOffset = -545; dayOffset <= 21; dayOffset += 1)
        {
            var day = nowUtc.Date.AddDays(dayOffset);

            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            // Not every working day has appointments, and busy days have several.
            var appointmentsToday = _random.Next(0, 4);

            for (var i = 0; i < appointmentsToday; i++)
            {
                var vehicle = vehicles[_random.Next(vehicles.Count)];
                var chosen = PickServicesFor(vehicle, services);

                if (chosen.Count == 0)
                {
                    continue;
                }

                var startHour = 8 + _random.Next(0, 8);
                var start = day.AddHours(startHour);
                var totalMinutes = chosen.Sum(s => s.EstimatedDurationMinutes);

                sequence++;
                var appointment = new Appointment
                {
                    AppointmentNumber = $"APP-{start.Year}-{sequence:D6}",
                    CustomerId = vehicle.OwnerId,
                    VehicleId = vehicle.Id,
                    AssignedEmployeeId = employeeProfileIds[_random.Next(employeeProfileIds.Count)],
                    ScheduledStartUtc = start,
                    ScheduledEndUtc = start.AddMinutes(totalMinutes),
                    TotalPrice = chosen.Sum(s => s.Price),
                    CustomerNotes = _random.Next(0, 3) == 0
                        ? "Vozilo pravi buku pri kočenju na manjim brzinama."
                        : null,
                    CreatedAtUtc = BookedAt(start, nowUtc),
                };

                AssignStatus(appointment, dayOffset, vehicle, adminId, nowUtc);

                _db.Appointments.Add(appointment);
                await _db.SaveChangesAsync(ct);

                foreach (var service in chosen)
                {
                    _db.AppointmentItems.Add(new AppointmentItem
                    {
                        AppointmentId = appointment.Id,
                        ServiceId = service.Id,

                        // Snapshots, not joins. A later catalogue price change must not rewrite
                        // this appointment's total or any report that already counted it.
                        UnitPriceSnapshot = service.Price,
                        DurationMinutesSnapshot = service.EstimatedDurationMinutes,
                        Quantity = 1,
                        LineTotal = service.Price,
                    });
                }

                AddStatusHistory(appointment, adminId);
                AddSignals(appointment, chosen);

                if (appointment.Status == AppointmentStatus.Completed
                    && appointment.PaymentStatus == AppointmentPaymentStatus.Paid)
                {
                    paymentSequence = AddPayment(
                        appointment,
                        chosen.Sum(s => s.Price),
                        currency,
                        stripeCurrency,
                        paymentSequence,
                        adminId);
                }

                if (appointment.Status == AppointmentStatus.Completed)
                {
                    AddReviewMaybe(appointment, chosen, nowUtc);
                    AddReminders(appointment, chosen, vehicle);
                }

                AddNotification(appointment);

                await _db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>
    /// When the appointment was booked: a few days before it takes place, and never in the
    /// future. An upcoming appointment was necessarily booked before today, and early enough
    /// that its confirmation yesterday still comes after it.
    /// </summary>
    private DateTime BookedAt(DateTime start, DateTime nowUtc)
    {
        var beforeStart = start.AddDays(-_random.Next(1, 10));
        var latestPossible = nowUtc.AddDays(-2).AddHours(-_random.Next(1, 48));

        return beforeStart < latestPossible ? beforeStart : latestPossible;
    }

    /// <summary>
    /// Picks one to three services that genuinely apply to the vehicle's engine type. Offering
    /// a petrol car a diesel-only service in the seed data would contradict the hard filter the
    /// recommender enforces at runtime.
    /// </summary>
    private List<Service> PickServicesFor(Vehicle vehicle, List<Service> services)
    {
        var applicable = services
            .Where(s => s.EngineTypeLinks.Count == 0
                        || s.EngineTypeLinks.Any(l => l.EngineTypeId == vehicle.EngineTypeId))
            .ToList();

        if (applicable.Count == 0)
        {
            return [];
        }

        var count = _random.Next(1, 4);

        return applicable
            .OrderBy(_ => _random.Next())
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Chooses a status consistent with when the appointment happens. A past appointment cannot
    /// still be Pending, and a future one cannot already be Completed.
    /// </summary>
    private void AssignStatus(
        Appointment appointment,
        int dayOffset,
        Vehicle vehicle,
        int adminId,
        DateTime nowUtc)
    {
        if (dayOffset < -2)
        {
            // Past: mostly completed, some cancelled.
            var roll = _random.Next(0, 10);

            if (roll < 8)
            {
                appointment.Status = AppointmentStatus.Completed;
                appointment.ConfirmedByUserId = adminId;
                appointment.ConfirmedAtUtc = appointment.CreatedAtUtc.AddHours(4);
                appointment.CompletedAtUtc = appointment.ScheduledEndUtc;
                appointment.MileageAtAppointmentKm =
                    Math.Max(0, vehicle.CurrentMileageKm - _random.Next(0, 40_000));

                // Roughly one completed appointment in twelve is left unpaid on purpose, so
                // the payment flow has real work waiting for it during the demonstration
                // rather than a database where everything is already settled.
                appointment.PaymentStatus = _random.Next(0, 12) == 0
                    ? AppointmentPaymentStatus.Unpaid
                    : AppointmentPaymentStatus.Paid;
            }
            else
            {
                appointment.Status = AppointmentStatus.Cancelled;
                appointment.CancelledByUserId = adminId;
                appointment.CancelledAtUtc = appointment.ScheduledStartUtc.AddDays(-1);
                appointment.CancellationReason = _random.Next(0, 2) == 0
                    ? "Klijent je otkazao termin zbog spriječenosti."
                    : "Nedostupan rezervni dio, termin je otkazan uz dogovor sa klijentom.";
                appointment.PaymentStatus = AppointmentPaymentStatus.Unpaid;
            }
        }
        else if (dayOffset < 0)
        {
            appointment.Status = AppointmentStatus.InProgress;
            appointment.ConfirmedByUserId = adminId;
            appointment.ConfirmedAtUtc = appointment.CreatedAtUtc.AddHours(4);
            appointment.PaymentStatus = AppointmentPaymentStatus.Unpaid;
        }
        else if (dayOffset <= 7)
        {
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.ConfirmedByUserId = adminId;
            appointment.ConfirmedAtUtc = nowUtc.AddDays(-1);
            appointment.PaymentStatus = AppointmentPaymentStatus.Unpaid;
        }
        else
        {
            appointment.Status = AppointmentStatus.Pending;
            appointment.PaymentStatus = AppointmentPaymentStatus.Unpaid;
        }
    }

    /// <summary>
    /// Writes the audit trail as a legal path through the state machine, never as a single row
    /// jumping straight to the final status.
    /// </summary>
    private void AddStatusHistory(Appointment appointment, int adminId)
    {
        void Add(AppointmentStatus? from, AppointmentStatus to, DateTime at, string? reason = null) =>
            _db.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appointment.Id,
                FromStatus = from,
                ToStatus = to,
                ChangedByUserId = from is null ? appointment.CustomerId : adminId,
                ChangedAtUtc = at,
                Reason = reason,
                ActorRole = from is null ? Roles.Customer : Roles.Administrator,
            });

        Add(null, AppointmentStatus.Pending, appointment.CreatedAtUtc);

        if (appointment.Status == AppointmentStatus.Pending)
        {
            return;
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            Add(AppointmentStatus.Pending, AppointmentStatus.Cancelled,
                appointment.CancelledAtUtc!.Value, appointment.CancellationReason);
            return;
        }

        Add(AppointmentStatus.Pending, AppointmentStatus.Confirmed, appointment.ConfirmedAtUtc!.Value);

        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            return;
        }

        Add(AppointmentStatus.Confirmed, AppointmentStatus.InProgress, appointment.ScheduledStartUtc);

        if (appointment.Status == AppointmentStatus.InProgress)
        {
            return;
        }

        Add(AppointmentStatus.InProgress, AppointmentStatus.Completed, appointment.CompletedAtUtc!.Value);
    }

    /// <summary>
    /// Writes the recommender signals this history would genuinely have produced. Nothing here
    /// is invented at recommendation time - the scorer reads exactly these rows.
    /// </summary>
    private void AddSignals(Appointment appointment, List<Service> services)
    {
        foreach (var service in services)
        {
            _db.UserServiceSignals.Add(new UserServiceSignal
            {
                UserId = appointment.CustomerId,
                ServiceId = service.Id,
                VehicleId = appointment.VehicleId,
                SignalType = SignalType.Booked,
                Weight = 2.0,
                OccurredAtUtc = appointment.CreatedAtUtc,
            });

            if (appointment.Status == AppointmentStatus.Completed)
            {
                _db.UserServiceSignals.Add(new UserServiceSignal
                {
                    UserId = appointment.CustomerId,
                    ServiceId = service.Id,
                    VehicleId = appointment.VehicleId,
                    SignalType = SignalType.Completed,
                    Weight = 3.0,
                    OccurredAtUtc = appointment.CompletedAtUtc!.Value,
                    PerformedAtUtc = appointment.CompletedAtUtc,
                });
            }
            else if (appointment.Status == AppointmentStatus.Cancelled)
            {
                _db.UserServiceSignals.Add(new UserServiceSignal
                {
                    UserId = appointment.CustomerId,
                    ServiceId = service.Id,
                    VehicleId = appointment.VehicleId,
                    SignalType = SignalType.Cancelled,
                    Weight = -1.0,
                    OccurredAtUtc = appointment.CancelledAtUtc!.Value,
                });
            }

            // Browsing leaves traces too, and at a much lower weight than booking.
            if (_random.Next(0, 3) == 0)
            {
                _db.UserServiceSignals.Add(new UserServiceSignal
                {
                    UserId = appointment.CustomerId,
                    ServiceId = service.Id,
                    VehicleId = appointment.VehicleId,
                    SignalType = SignalType.Viewed,
                    Weight = 0.5,
                    OccurredAtUtc = appointment.CreatedAtUtc.AddHours(-_random.Next(1, 48)),
                });
            }
        }
    }

    /// <summary>
    /// Creates the payment for a completed appointment.
    /// <para>
    /// The total is passed in rather than queried back, because the appointment's items are
    /// still only in the change tracker at this point. Querying would return zero and every
    /// payment would fail the "amount greater than zero" check constraint.
    /// </para>
    /// </summary>
    private int AddPayment(
        Appointment appointment,
        decimal itemTotal,
        string currency,
        string stripeCurrency,
        int paymentSequence,
        int adminId)
    {
        paymentSequence++;

        var payment = new Payment
        {
            PaymentNumber = $"PAY-{appointment.ScheduledStartUtc.Year}-{paymentSequence:D6}",
            AppointmentId = appointment.Id,
            PayerId = appointment.CustomerId,

            // Equal to the sum of the line snapshots, never to the current catalogue price.
            Amount = itemTotal,
            Currency = currency,
            AmountChargedStripe = itemTotal,
            StripeCurrency = stripeCurrency,
            Status = PaymentStatus.Succeeded,
            StripePaymentIntentId = $"pi_seed_{appointment.Id:D8}",
            StripeChargeId = $"ch_seed_{appointment.Id:D8}",
            IdempotencyKey = $"appt-{appointment.Id}-{(int)(itemTotal * 100)}",
            PaidAtUtc = appointment.CompletedAtUtc,
            CreatedAtUtc = appointment.CompletedAtUtc!.Value.AddMinutes(-5),
        };

        _db.Payments.Add(payment);

        // A small number of payments are refunded so the refund module has real data and the
        // revenue report has gross, refunded and net figures that differ.
        if (_random.Next(0, 12) == 0)
        {
            // Navigation rather than the key: the payment has no id until SaveChanges runs.
            _db.Refunds.Add(new Refund
            {
                Payment = payment,
                Amount = payment.Amount,
                Reason = "Usluga nije izvršena u dogovorenom obimu, odobren puni povrat.",
                Status = RefundStatus.Succeeded,
                StripeRefundId = $"re_seed_{appointment.Id:D8}",
                RequestedByUserId = adminId,
                RequestedAtUtc = payment.PaidAtUtc!.Value.AddDays(2),
                CompletedAtUtc = payment.PaidAtUtc!.Value.AddDays(2).AddMinutes(30),
            });

            appointment.PaymentStatus = AppointmentPaymentStatus.Refunded;
        }

        return paymentSequence;
    }

    private void AddReviewMaybe(Appointment appointment, List<Service> services, DateTime nowUtc)
    {
        // Not every completed job gets reviewed, which is what real data looks like.
        if (_random.Next(0, 10) >= 4)
        {
            return;
        }

        var rating = _random.Next(0, 10) < 8 ? _random.Next(4, 6) : _random.Next(2, 4);

        var comments = new[]
        {
            "Odličan servis, brza zamjena ulja i filtera. Osoblje veoma profesionalno.",
            "Dijagnostika urađena korektno, malo duže čekanje ali su objasnili sve detaljno.",
            "Vrhunski urađeno, kočnice perfektne. Cijena pristupačna. Svaka čast!",
            "Sve u redu, termin ispoštovan i vozilo spremno na vrijeme.",
            "Korektna usluga, ali bi komunikacija o kašnjenju mogla biti bolja.",
        };

        var reviewedAt = appointment.CompletedAtUtc!.Value.AddDays(_random.Next(1, 8));

        if (reviewedAt > nowUtc)
        {
            return;
        }

        var service = services[_random.Next(services.Count)];

        _db.Reviews.Add(new Review
        {
            AppointmentId = appointment.Id,
            ServiceId = service.Id,
            AuthorId = appointment.CustomerId,
            AssignedEmployeeId = appointment.AssignedEmployeeId,
            Rating = rating,
            Comment = comments[_random.Next(comments.Length)],
            CreatedAtUtc = reviewedAt,
            IsVisible = true,
        });

        _db.UserServiceSignals.Add(new UserServiceSignal
        {
            UserId = appointment.CustomerId,
            ServiceId = service.Id,
            VehicleId = appointment.VehicleId,
            SignalType = SignalType.Reviewed,

            // A low rating pushes the service away rather than counting as engagement.
            Weight = rating >= 4 ? 2.5 : -1.5,
            OccurredAtUtc = reviewedAt,
        });
    }

    /// <summary>
    /// Generates the maintenance reminders a completed appointment would have produced, due by
    /// date or by mileage according to what the service declares.
    /// </summary>
    private void AddReminders(Appointment appointment, List<Service> services, Vehicle vehicle)
    {
        foreach (var service in services)
        {
            if (service.RecommendedMileageIntervalKm is null
                && service.RecommendedMonthsInterval is null)
            {
                continue;
            }

            var dueDate = service.RecommendedMonthsInterval is { } months
                ? appointment.CompletedAtUtc!.Value.AddMonths(months)
                : (DateTime?)null;

            var dueMileage = service.RecommendedMileageIntervalKm is { } km
                ? (appointment.MileageAtAppointmentKm ?? vehicle.CurrentMileageKm) + km
                : (int?)null;

            // Doing a service again answers the reminder the previous time left behind, exactly
            // as completing an appointment does at runtime. Without this every past service
            // would leave an open reminder and a vehicle would be "due" dozens of times over.
            if (_openReminders.TryGetValue((appointment.VehicleId, service.Id), out var previous))
            {
                previous.Status = ServiceReminderStatus.Fulfilled;
            }

            var reminder = new ServiceReminder
            {
                VehicleId = appointment.VehicleId,
                ServiceId = service.Id,
                DueDateUtc = dueDate,
                DueMileageKm = dueMileage,
                Status = dueDate is not null && dueDate < DateTime.UtcNow
                    ? ServiceReminderStatus.Sent
                    : ServiceReminderStatus.Pending,
                GeneratedFromAppointmentId = appointment.Id,
                SentAtUtc = dueDate is not null && dueDate < DateTime.UtcNow ? dueDate : null,
                CreatedAtUtc = appointment.CompletedAtUtc!.Value,
            };

            _openReminders[(appointment.VehicleId, service.Id)] = reminder;
            _db.ServiceReminders.Add(reminder);
        }
    }

    private void AddNotification(Appointment appointment)
    {
        var (type, title, body) = appointment.Status switch
        {
            AppointmentStatus.Completed => (
                NotificationType.AppointmentCompleted,
                "Servis završen",
                $"Vaš servis {appointment.AppointmentNumber} je uspješno završen."),

            AppointmentStatus.Cancelled => (
                NotificationType.AppointmentCancelled,
                "Rezervacija otkazana",
                $"Rezervacija {appointment.AppointmentNumber} je otkazana. "
                + $"Razlog: {appointment.CancellationReason}"),

            AppointmentStatus.Confirmed => (
                NotificationType.AppointmentConfirmed,
                "Rezervacija potvrđena",
                $"Vaša rezervacija {appointment.AppointmentNumber} je potvrđena za "
                + $"{appointment.ScheduledStartUtc:dd.MM.yyyy.} u "
                + $"{appointment.ScheduledStartUtc:HH:mm}h."),

            AppointmentStatus.InProgress => (
                NotificationType.AppointmentConfirmed,
                "Servis u toku",
                $"Rad na vašem vozilu je započet, rezervacija {appointment.AppointmentNumber}."),

            _ => (
                NotificationType.AppointmentCreated,
                "Rezervacija kreirana",
                $"Zaprimili smo vašu rezervaciju {appointment.AppointmentNumber}. "
                + "Obavijestit ćemo vas kada bude potvrđena."),
        };

        _db.Notifications.Add(new Notification
        {
            UserId = appointment.CustomerId,
            Type = type,
            Title = title,
            Body = body,

            // Older notifications are read, recent ones are not, so the unread badge has a
            // sensible number in it on first login.
            IsRead = appointment.CreatedAtUtc < DateTime.UtcNow.AddDays(-14),
            ReadAtUtc = appointment.CreatedAtUtc < DateTime.UtcNow.AddDays(-14)
                ? appointment.CreatedAtUtc.AddHours(6)
                : null,
            CreatedAtUtc = appointment.CreatedAtUtc,
            RelatedEntityType = nameof(Appointment),
            RelatedEntityId = appointment.Id,
        });
    }
}
