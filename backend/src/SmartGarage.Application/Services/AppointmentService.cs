using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Options;
using SmartGarage.Contracts.Events;
using SmartGarage.Contracts.Routing;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;
using SmartGarage.Domain.StateMachine;

namespace SmartGarage.Application.Services;

public interface IAppointmentService
{
    Task<PagedResult<AppointmentListItemResponse>> GetAsync(AppointmentSearchRequest search, CancellationToken ct);

    Task<AppointmentDetailResponse> GetByIdAsync(int id, CancellationToken ct);

    Task<AppointmentSummaryResponse> GetSummaryAsync(CancellationToken ct);

    Task<AppointmentDetailResponse> CreateAsync(AppointmentCreateRequest request, CancellationToken ct);

    Task<AppointmentDetailResponse> ConfirmAsync(int id, CancellationToken ct);

    Task<AppointmentDetailResponse> StartAsync(int id, CancellationToken ct);

    Task<AppointmentDetailResponse> CompleteAsync(int id, AppointmentCompleteRequest request, CancellationToken ct);

    Task<AppointmentDetailResponse> CancelAsync(int id, AppointmentCancelRequest request, CancellationToken ct);

    Task<AppointmentDetailResponse> RescheduleAsync(int id, AppointmentRescheduleRequest request, CancellationToken ct);

    Task<AppointmentDetailResponse> AssignAsync(int id, AppointmentAssignRequest request, CancellationToken ct);

    /// <summary>
    /// Cancels bookings whose time has come without anyone confirming them. Run by the Worker
    /// on a schedule; returns how many were cancelled.
    /// </summary>
    Task<int> ExpireUnconfirmedAsync(CancellationToken ct);
}

/// <summary>
/// Appointments: booking, the status workflow, rescheduling and assignment.
/// <para>
/// <see cref="TransitionAsync"/> is the only place in the code base that assigns
/// <see cref="Appointment.Status"/>. Every status endpoint is a call into it, and it asks
/// <see cref="AppointmentStateMachine"/> before it changes anything. A new appointment starts
/// as Pending through the entity's default, not through an assignment here.
/// </para>
/// </summary>
public sealed class AppointmentService : IAppointmentService
{
    private const string DateTimeFormat = "dd.MM.yyyy. HH:mm";
    private const string SystemActorName = "Sistem";
    private const string ExpiryReason =
        "Rezervacija je automatski otkazana jer nije potvrđena do zakazanog termina.";

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly ISlotPlanner _planner;
    private readonly IEventPublisher _events;
    private readonly BookingOptions _booking;

    public AppointmentService(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        ISlotPlanner planner,
        IEventPublisher events,
        IOptions<BookingOptions> booking)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _planner = planner;
        _events = events;
        _booking = booking.Value;
    }

    private bool IsCustomer => _currentUser.IsInRole(Roles.Customer);

    private bool IsAdministrator => _currentUser.IsInRole(Roles.Administrator);

    private string ActorRole => _currentUser.Roles.FirstOrDefault() ?? string.Empty;

    // Ownership lives in the query: a customer's queries contain only their own appointments,
    // so someone else's is simply not found.
    private IQueryable<Appointment> Scope
    {
        get
        {
            if (!IsCustomer)
            {
                return _db.Set<Appointment>();
            }

            var userId = _currentUser.RequireUserId();
            return _db.Set<Appointment>().Where(a => a.CustomerId == userId);
        }
    }

    private IQueryable<Appointment> Active =>
        _db.Set<Appointment>().Where(
            a => a.Status != AppointmentStatus.Completed && a.Status != AppointmentStatus.Cancelled);

    public async Task<PagedResult<AppointmentListItemResponse>> GetAsync(
        AppointmentSearchRequest search,
        CancellationToken ct)
    {
        var query = Scope.AsNoTracking();

        if (search.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        if (search.From is { } from)
        {
            query = query.Where(a => a.ScheduledStartUtc >= from);
        }

        if (search.To is { } to)
        {
            query = query.Where(a => a.ScheduledStartUtc <= to);
        }

        if (search.VehicleId is { } vehicleId)
        {
            query = query.Where(a => a.VehicleId == vehicleId);
        }

        if (search.EmployeeId is { } employeeId)
        {
            query = query.Where(a => a.AssignedEmployeeId == employeeId);
        }

        // For a customer the scope already limits the query to their own, so this cannot
        // widen what they see.
        if (search.CustomerId is { } customerId)
        {
            query = query.Where(a => a.CustomerId == customerId);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();

            // Translated to SQL LIKE; how text compares is the column collation's business.
#pragma warning disable CA1307
            query = query.Where(a =>
                a.AppointmentNumber.Contains(term)
                || a.Customer.FirstName.Contains(term)
                || a.Customer.LastName.Contains(term)
                || a.Vehicle.RegistrationPlate.Contains(term)
                || a.Items.Any(i => i.Service.Name.Contains(term)));
#pragma warning restore CA1307
        }

        var totalCount = await query.CountAsync(ct);

        var ordered = string.Equals(search.OrderBy, "start", StringComparison.OrdinalIgnoreCase) && !search.Descending
            ? query.OrderBy(a => a.ScheduledStartUtc).ThenBy(a => a.Id)
            : query.OrderByDescending(a => a.ScheduledStartUtc).ThenByDescending(a => a.Id);

        var items = await ordered
            .Skip(search.Skip)
            .Take(search.ClampedPageSize)
            .Select(a => new AppointmentListItemResponse
            {
                Id = a.Id,
                AppointmentNumber = a.AppointmentNumber,
                ScheduledStartUtc = a.ScheduledStartUtc,
                ScheduledEndUtc = a.ScheduledEndUtc,
                Status = a.Status,
                PaymentStatus = a.PaymentStatus,
                TotalPrice = a.TotalPrice,
                VehicleId = a.VehicleId,
                VehicleDisplay = a.Vehicle.Brand.Name + " " + a.Vehicle.Model.Name
                                 + " (" + a.Vehicle.RegistrationPlate + ")",
                CustomerFullName = a.Customer.FirstName + " " + a.Customer.LastName,
                AssignedEmployeeName = a.AssignedEmployee == null
                    ? null
                    : a.AssignedEmployee.User.FirstName + " " + a.AssignedEmployee.User.LastName,
                ServiceNames = a.Items.Select(i => i.Service.Name).ToList(),
            })
            .ToListAsync(ct);

        return new PagedResult<AppointmentListItemResponse>(
            items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<AppointmentDetailResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var detail = await Scope
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new AppointmentDetailResponse
                {
                    Id = a.Id,
                    AppointmentNumber = a.AppointmentNumber,
                    ScheduledStartUtc = a.ScheduledStartUtc,
                    ScheduledEndUtc = a.ScheduledEndUtc,
                    Status = a.Status,
                    PaymentStatus = a.PaymentStatus,
                    TotalPrice = a.TotalPrice,
                    VehicleId = a.VehicleId,
                    VehicleDisplay = a.Vehicle.Brand.Name + " " + a.Vehicle.Model.Name
                                     + " (" + a.Vehicle.RegistrationPlate + ")",
                    CustomerFullName = a.Customer.FirstName + " " + a.Customer.LastName,
                    AssignedEmployeeName = a.AssignedEmployee == null
                        ? null
                        : a.AssignedEmployee.User.FirstName + " " + a.AssignedEmployee.User.LastName,
                    AssignedEmployeePhone = a.AssignedEmployee == null ? null : a.AssignedEmployee.User.PhoneNumber,
                    ServiceNames = a.Items.Select(i => i.Service.Name).ToList(),
                    CustomerNotes = a.CustomerNotes,
                    InternalNotes = a.InternalNotes,
                    MileageAtAppointmentKm = a.MileageAtAppointmentKm,
                    CancellationReason = a.CancellationReason,
                    ConfirmedAtUtc = a.ConfirmedAtUtc,
                    CompletedAtUtc = a.CompletedAtUtc,
                    CancelledAtUtc = a.CancelledAtUtc,
                    HasReview = a.Review != null,
                    Items = a.Items
                        .Select(i => new AppointmentItemResponse
                        {
                            ServiceId = i.ServiceId,
                            ServiceName = i.Service.Name,
                            UnitPrice = i.UnitPriceSnapshot,
                            DurationMinutes = i.DurationMinutesSnapshot,
                            Quantity = i.Quantity,
                            LineTotal = i.LineTotal,
                        })
                        .ToList(),
                    History = a.History
                        .OrderBy(h => h.ChangedAtUtc)
                        .ThenBy(h => h.Id)
                        .Select(h => new AppointmentHistoryResponse
                        {
                            FromStatus = h.FromStatus,
                            ToStatus = h.ToStatus,
                            ChangedAtUtc = h.ChangedAtUtc,
                            ChangedByName = h.ChangedByUser == null
                                ? SystemActorName
                                : h.ChangedByUser.FirstName + " " + h.ChangedByUser.LastName,
                            ActorRole = h.ActorRole,
                            Reason = h.Reason,
                        })
                        .ToList(),
                })
                .SingleOrDefaultAsync(ct)
            ?? throw NotFound();

        DescribeActions(detail);
        return detail;
    }

    // One GROUP BY in the database, not six COUNT queries and not a list counted in memory.
    public async Task<AppointmentSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var counts = await Scope
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, ct);

        return new AppointmentSummaryResponse
        {
            Total = counts.Values.Sum(),
            Pending = counts.GetValueOrDefault(AppointmentStatus.Pending),
            Confirmed = counts.GetValueOrDefault(AppointmentStatus.Confirmed),
            InProgress = counts.GetValueOrDefault(AppointmentStatus.InProgress),
            Completed = counts.GetValueOrDefault(AppointmentStatus.Completed),
            Cancelled = counts.GetValueOrDefault(AppointmentStatus.Cancelled),
        };
    }

    public async Task<AppointmentDetailResponse> CreateAsync(AppointmentCreateRequest request, CancellationToken ct)
    {
        var customerId = _currentUser.RequireUserId();
        var start = DateTime.SpecifyKind(request.ScheduledStartUtc, DateTimeKind.Utc);
        var serviceIds = request.ServiceIds.Distinct().ToList();

        // Serializable, so the availability that is read stays true until the insert commits.
        // Two customers going for the last place cannot both find it free.
        var created = await _db.ExecuteInTransactionAsync(
            async token =>
            {
                _db.DiscardChanges();

                var vehicle = await _db.Set<Vehicle>()
                        .Where(v => v.Id == request.VehicleId && v.OwnerId == customerId)
                        .Select(v => new { v.Id, v.EngineTypeId })
                        .SingleOrDefaultAsync(token)
                    ?? throw new ValidationException("vehicleId", "Izabrano vozilo ne postoji među vašim vozilima.");

                // Price and duration come from the catalogue as it is at this moment. Nothing
                // the client sent about either is used, or even accepted.
                var services = await _db.Set<Service>()
                    .Where(s => serviceIds.Contains(s.Id) && s.IsActive)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Price,
                        s.EstimatedDurationMinutes,
                        EngineTypeIds = s.EngineTypeLinks.Select(l => l.EngineTypeId).ToList(),
                    })
                    .ToListAsync(token);

                if (services.Count != serviceIds.Count)
                {
                    throw new ValidationException(
                        "serviceIds", "Jedna ili više izabranih usluga više nije dostupna.");
                }

                var unsuitable = services.FirstOrDefault(
                    s => s.EngineTypeIds.Count > 0 && !s.EngineTypeIds.Contains(vehicle.EngineTypeId));

                if (unsuitable is not null)
                {
                    throw new ValidationException(
                        "serviceIds", $"Usluga „{unsuitable.Name}“ nije predviđena za tip motora ovog vozila.");
                }

                var end = await EnsureSlotIsFreeAsync(
                    start,
                    services.Sum(s => s.EstimatedDurationMinutes),
                    vehicle.Id,
                    customerId,
                    exceptAppointmentId: null,
                    token);

                var now = _clock.UtcNow;

                var appointment = new Appointment
                {
                    AppointmentNumber = await NextNumberAsync(now.Year, token),
                    CustomerId = customerId,
                    VehicleId = vehicle.Id,
                    ScheduledStartUtc = start,
                    ScheduledEndUtc = end,
                    TotalPrice = services.Sum(s => s.Price),
                    CustomerNotes = string.IsNullOrWhiteSpace(request.CustomerNotes) ? null : request.CustomerNotes.Trim(),
                    CreatedAtUtc = now,
                    Items = services
                        .Select(s => new AppointmentItem
                        {
                            ServiceId = s.Id,

                            // Snapshots: a later change to the catalogue price must not alter
                            // what this customer agreed to, or any report that counted it.
                            UnitPriceSnapshot = s.Price,
                            DurationMinutesSnapshot = s.EstimatedDurationMinutes,
                            Quantity = 1,
                            LineTotal = s.Price,
                        })
                        .ToList(),
                    History =
                    [
                        new AppointmentStatusHistory
                        {
                            FromStatus = null,
                            ToStatus = AppointmentStatus.Pending,
                            ChangedByUserId = customerId,
                            ChangedAtUtc = now,
                            ActorRole = Roles.Customer,
                        },
                    ],
                };

                _db.Set<Appointment>().Add(appointment);
                AddSignals(appointment, SignalType.Booked, SignalWeights.Booked, now);

                await _db.SaveChangesAsync(token);

                return new { appointment.Id, appointment.AppointmentNumber };
            },
            IsolationLevel.Serializable,
            ct);

        var detail = await GetByIdAsync(created.Id, ct);

        await _events.PublishAsync(
            new AppointmentCreatedEvent
            {
                AppointmentId = created.Id,
                AppointmentNumber = created.AppointmentNumber,
                CustomerId = customerId,
                CustomerFullName = detail.CustomerFullName,
                ScheduledStartUtc = start,
                CorrelationId = _currentUser.CorrelationId,
            },
            MessageRouting.RoutingKeys.AppointmentCreated,
            ct);

        return detail;
    }

    public Task<AppointmentDetailResponse> ConfirmAsync(int id, CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.Confirmed, reason: null, completion: null, ct);

    public Task<AppointmentDetailResponse> StartAsync(int id, CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.InProgress, reason: null, completion: null, ct);

    public Task<AppointmentDetailResponse> CompleteAsync(
        int id,
        AppointmentCompleteRequest request,
        CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.Completed, reason: null, request, ct);

    public Task<AppointmentDetailResponse> CancelAsync(int id, AppointmentCancelRequest request, CancellationToken ct) =>
        TransitionAsync(id, AppointmentStatus.Cancelled, request.Reason, completion: null, ct);

    public async Task<AppointmentDetailResponse> RescheduleAsync(
        int id,
        AppointmentRescheduleRequest request,
        CancellationToken ct)
    {
        var newStart = DateTime.SpecifyKind(request.NewStartUtc, DateTimeKind.Utc);
        var userId = _currentUser.RequireUserId();
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        var moved = await _db.ExecuteInTransactionAsync(
            async token =>
            {
                _db.DiscardChanges();

                var appointment = await Scope
                        .Include(a => a.Items)
                        .Include(a => a.Customer)
                        .SingleOrDefaultAsync(a => a.Id == id, token)
                    ?? throw NotFound();

                if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
                {
                    throw new ConflictException(
                        "Termin se može promijeniti samo dok je rezervacija na čekanju ili potvrđena.");
                }

                EnsureCustomerIsNotTooLate(appointment);

                var oldStart = appointment.ScheduledStartUtc;

                if (oldStart == newStart)
                {
                    throw new ValidationException("newStartUtc", "Novi termin je isti kao postojeći.");
                }

                var end = await EnsureSlotIsFreeAsync(
                    newStart,
                    appointment.Items.Sum(i => i.DurationMinutesSnapshot * i.Quantity),
                    appointment.VehicleId,
                    appointment.CustomerId,
                    appointment.Id,
                    token);

                if (appointment.AssignedEmployeeId is { } employeeId)
                {
                    await EnsureEmployeeIsFreeAsync(employeeId, newStart, end, appointment.Id, token);
                }

                appointment.ScheduledStartUtc = newStart;
                appointment.ScheduledEndUtc = end;

                // The status stays what it was; the row records who moved the appointment,
                // when, from where to where, and why.
                appointment.History.Add(new AppointmentStatusHistory
                {
                    FromStatus = appointment.Status,
                    ToStatus = appointment.Status,
                    ChangedByUserId = userId,
                    ChangedAtUtc = _clock.UtcNow,
                    ActorRole = ActorRole,
                    Reason = $"Termin pomjeren sa {Format(oldStart)} na {Format(newStart)}."
                             + (reason is null ? string.Empty : $" {reason}"),
                });

                await _db.SaveChangesAsync(token);

                return new AppointmentRescheduledEvent
                {
                    AppointmentId = appointment.Id,
                    AppointmentNumber = appointment.AppointmentNumber,
                    CustomerId = appointment.CustomerId,
                    CustomerFullName = appointment.Customer.FullName,
                    OldStartUtc = oldStart,
                    NewStartUtc = newStart,
                    Reason = reason,
                    ChangedByCustomer = IsCustomer,
                    CorrelationId = _currentUser.CorrelationId,
                };
            },
            IsolationLevel.Serializable,
            ct);

        await _events.PublishAsync(moved, MessageRouting.RoutingKeys.AppointmentRescheduled, ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<AppointmentDetailResponse> AssignAsync(
        int id,
        AppointmentAssignRequest request,
        CancellationToken ct)
    {
        var appointment = await Scope.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw NotFound();

        if (AppointmentStateMachine.IsTerminal(appointment.Status))
        {
            throw new ConflictException("Mehaničar se ne može dodijeliti završenoj ili otkazanoj rezervaciji.");
        }

        var weekday = appointment.ScheduledStartUtc.DayOfWeek;
        var startTime = TimeOnly.FromDateTime(appointment.ScheduledStartUtc);

        var employee = await _db.Set<EmployeeProfile>()
                .Where(e => e.Id == request.EmployeeId)
                .Select(e => new
                {
                    e.IsAvailableForAssignment,
                    e.User.IsActive,
                    WorksThen = e.Schedules.Any(
                        s => s.DayOfWeek == weekday
                             && s.IsWorkingDay
                             && s.StartTime <= startTime
                             && startTime < s.EndTime),
                })
                .SingleOrDefaultAsync(ct)
            ?? throw new ValidationException("employeeId", "Izabrani zaposlenik ne postoji.");

        if (!employee.IsActive || !employee.IsAvailableForAssignment)
        {
            throw new ValidationException("employeeId", "Izabrani zaposlenik trenutno ne prima nove poslove.");
        }

        if (!employee.WorksThen)
        {
            throw new ValidationException("employeeId", "Izabrani zaposlenik ne radi u vrijeme ovog termina.");
        }

        await EnsureEmployeeIsFreeAsync(
            request.EmployeeId, appointment.ScheduledStartUtc, appointment.ScheduledEndUtc, appointment.Id, ct);

        appointment.AssignedEmployeeId = request.EmployeeId;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<int> ExpireUnconfirmedAsync(CancellationToken ct)
    {
        if (!_currentUser.IsInRole(Roles.System))
        {
            throw new ForbiddenException("Ovu radnju izvršava samo sistem.");
        }

        var now = _clock.UtcNow;

        var overdue = await _db.Set<Appointment>()
            .Where(a => a.Status == AppointmentStatus.Pending && a.ScheduledStartUtc <= now)
            .Select(a => a.Id)
            .ToListAsync(ct);

        // Through the same transition as any other cancellation: the state machine is asked,
        // the history row is written and the customer is notified.
        foreach (var id in overdue)
        {
            await TransitionAsync(id, AppointmentStatus.Cancelled, ExpiryReason, completion: null, ct);
        }

        return overdue.Count;
    }

    /// <summary>
    /// Moves an appointment to another status. The only code that writes the status.
    /// </summary>
    private async Task<AppointmentDetailResponse> TransitionAsync(
        int id,
        AppointmentStatus target,
        string? reason,
        AppointmentCompleteRequest? completion,
        CancellationToken ct)
    {
        // Null when the Worker is acting on a schedule; the history then records the system.
        var userId = _currentUser.UserId;

        var appointment = await Scope
                .Include(a => a.Items).ThenInclude(i => i.Service)
                .Include(a => a.Vehicle)
                .Include(a => a.Customer)
                .SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw NotFound();

        var from = appointment.Status;

        var rule = AppointmentStateMachine.FindRule(from, target)
            ?? throw new ConflictException(
                $"Rezervacija u statusu „{Display(from)}“ ne može preći u status „{Display(target)}“.");

        if (!_currentUser.Roles.Any(rule.AllowedRoles.Contains))
        {
            throw new ForbiddenException("Nemate dozvolu za ovu promjenu statusa rezervacije.");
        }

        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (rule.RequiresReason && reason is null)
        {
            throw new ValidationException("reason", "Navedite razlog otkazivanja.");
        }

        var now = _clock.UtcNow;

        switch (target)
        {
            case AppointmentStatus.Confirmed:
                appointment.ConfirmedByUserId = userId;
                appointment.ConfirmedAtUtc = now;
                break;

            case AppointmentStatus.InProgress:
                EnsureWorkCanStart(appointment, now);
                break;

            case AppointmentStatus.Completed:
                await ApplyCompletionAsync(appointment, completion!, now, ct);
                break;

            case AppointmentStatus.Cancelled:
                EnsureCustomerIsNotTooLate(appointment);
                appointment.CancellationReason = reason;
                appointment.CancelledByUserId = userId;
                appointment.CancelledAtUtc = now;
                AddSignals(appointment, SignalType.Cancelled, SignalWeights.Cancelled, now);
                break;

            default:
                throw new InvalidOperationException($"No transition into {target} is defined.");
        }

        appointment.Status = target;

        // The audit trail the instructions ask for: who, when, from what to what, and why.
        appointment.History.Add(new AppointmentStatusHistory
        {
            FromStatus = from,
            ToStatus = target,
            ChangedByUserId = userId,
            ChangedAtUtc = now,
            ActorRole = ActorRole,
            Reason = reason,
        });

        await _db.SaveChangesAsync(ct);

        await _events.PublishAsync(
            new AppointmentStatusChangedEvent
            {
                AppointmentId = appointment.Id,
                AppointmentNumber = appointment.AppointmentNumber,
                CustomerId = appointment.CustomerId,
                CustomerFullName = appointment.Customer.FullName,
                FromStatus = from.ToString(),
                ToStatus = target.ToString(),
                Reason = reason,
                ScheduledStartUtc = appointment.ScheduledStartUtc,
                ChangedByCustomer = IsCustomer,
                CorrelationId = _currentUser.CorrelationId,
            },
            MessageRouting.RoutingKeys.AppointmentStatusChanged,
            ct);

        return await GetByIdAsync(id, ct);
    }

    private static void EnsureWorkCanStart(Appointment appointment, DateTime now)
    {
        if (appointment.AssignedEmployeeId is null)
        {
            throw new BusinessRuleException("Prije početka rada rezervaciji dodijelite mehaničara.");
        }

        if (now.Date < appointment.ScheduledStartUtc.Date)
        {
            throw new BusinessRuleException(
                "Rad ne može početi prije dana za koji je rezervacija zakazana.");
        }
    }

    private async Task ApplyCompletionAsync(
        Appointment appointment,
        AppointmentCompleteRequest completion,
        DateTime now,
        CancellationToken ct)
    {
        if (completion.MileageAtAppointmentKm < appointment.Vehicle.CurrentMileageKm)
        {
            throw new ValidationException(
                "mileageAtAppointmentKm",
                $"Kilometraža ne može biti manja od trenutno upisane ({appointment.Vehicle.CurrentMileageKm} km).");
        }

        appointment.MileageAtAppointmentKm = completion.MileageAtAppointmentKm;
        appointment.CompletedAtUtc = now;

        if (!string.IsNullOrWhiteSpace(completion.InternalNotes))
        {
            appointment.InternalNotes = completion.InternalNotes.Trim();
        }

        appointment.Vehicle.CurrentMileageKm = completion.MileageAtAppointmentKm;
        appointment.Vehicle.LastMileageUpdatedAtUtc = now;

        AddSignals(appointment, SignalType.Completed, SignalWeights.Completed, now);

        var serviceIds = appointment.Items.Select(i => i.ServiceId).ToList();

        // Doing the service again answers the reminder that asked for it.
        var answered = await _db.Set<ServiceReminder>()
            .Where(r => r.VehicleId == appointment.VehicleId
                        && serviceIds.Contains(r.ServiceId)
                        && (r.Status == ServiceReminderStatus.Pending || r.Status == ServiceReminderStatus.Sent))
            .ToListAsync(ct);

        foreach (var reminder in answered)
        {
            reminder.Status = ServiceReminderStatus.Fulfilled;
        }

        foreach (var service in appointment.Items.Select(i => i.Service))
        {
            if (service.RecommendedMonthsInterval is null && service.RecommendedMileageIntervalKm is null)
            {
                continue;
            }

            _db.Set<ServiceReminder>().Add(new ServiceReminder
            {
                VehicleId = appointment.VehicleId,
                ServiceId = service.Id,
                DueDateUtc = service.RecommendedMonthsInterval is { } months ? now.AddMonths(months) : null,
                DueMileageKm = service.RecommendedMileageIntervalKm is { } km
                    ? completion.MileageAtAppointmentKm + km
                    : null,
                GeneratedFromAppointment = appointment,
                CreatedAtUtc = now,
            });
        }
    }

    /// <summary>
    /// Every server-side check a time must pass, for a new booking and for a moved one alike.
    /// Returns when the appointment would end.
    /// </summary>
    private async Task<DateTime> EnsureSlotIsFreeAsync(
        DateTime start,
        int durationMinutes,
        int vehicleId,
        int customerId,
        int? exceptAppointmentId,
        CancellationToken ct)
    {
        if (!_planner.IsSlotStart(start))
        {
            throw new ValidationException(
                exceptAppointmentId is null ? "scheduledStartUtc" : "newStartUtc",
                "Termin mora početi na pun sat unutar radnog vremena servisa.");
        }

        var workingDays = await _planner.LoadWorkingDaysAsync(ct);
        var end = _planner.EndOf(start, durationMinutes, workingDays);
        var booked = await _planner.LoadBookedAsync(start, end, exceptAppointmentId, ct);
        var state = _planner.Evaluate(start, end, workingDays, booked);

        if (!state.IsAvailable)
        {
            throw new ConflictException(state.UnavailableReason!);
        }

        var others = Active.Where(a => a.Id != exceptAppointmentId);

        if (await others.AnyAsync(
                a => a.VehicleId == vehicleId && a.ScheduledStartUtc < end && start < a.ScheduledEndUtc, ct))
        {
            throw new ConflictException(
                "Ovo vozilo već ima rezervaciju koja se preklapa sa izabranim terminom.");
        }

        // The duplicate the instructions name explicitly: same customer, same time.
        return await others.AnyAsync(a => a.CustomerId == customerId && a.ScheduledStartUtc == start, ct)
            ? throw new ConflictException("Već imate rezervaciju u ovom terminu.")
            : end;
    }

    private async Task EnsureEmployeeIsFreeAsync(
        int employeeId,
        DateTime start,
        DateTime end,
        int exceptAppointmentId,
        CancellationToken ct)
    {
        // A mechanic is not handed the next vehicle the minute the previous one leaves.
        var buffer = TimeSpan.FromMinutes(_booking.BufferMinutes);
        var from = start - buffer;
        var to = end + buffer;

        if (await Active.AnyAsync(
                a => a.Id != exceptAppointmentId
                     && a.AssignedEmployeeId == employeeId
                     && a.ScheduledStartUtc < to
                     && from < a.ScheduledEndUtc,
                ct))
        {
            throw new ConflictException("Mehaničar već ima posao koji se preklapa sa ovim terminom.");
        }
    }

    // Staff can always step in; the cut-off protects the garage's plan from late changes by
    // the customer only.
    private void EnsureCustomerIsNotTooLate(Appointment appointment)
    {
        if (IsCustomer && IsInsideCutoff(appointment.ScheduledStartUtc))
        {
            throw new BusinessRuleException(CutoffMessage());
        }
    }

    private bool IsInsideCutoff(DateTime start) =>
        start - _clock.UtcNow < TimeSpan.FromHours(_booking.CancellationCutoffHours);

    private string CutoffMessage() =>
        $"Rezervaciju više nije moguće otkazati ni pomjeriti jer do termina ima manje od "
        + $"{_booking.CancellationCutoffHours} sati. Kontaktirajte servis.";

    private void AddSignals(Appointment appointment, SignalType type, double weight, DateTime now)
    {
        foreach (var item in appointment.Items)
        {
            _db.Set<UserServiceSignal>().Add(new UserServiceSignal
            {
                UserId = appointment.CustomerId,
                ServiceId = item.ServiceId,
                VehicleId = appointment.VehicleId,
                SignalType = type,
                Weight = weight,
                OccurredAtUtc = now,
                PerformedAtUtc = type == SignalType.Completed ? now : null,
            });
        }
    }

    // Numbers are issued inside the booking transaction, and the unique index on the column
    // is the final guard against two appointments ever sharing one.
    private async Task<string> NextNumberAsync(int year, CancellationToken ct)
    {
        var prefix = $"APP-{year}-";

        var last = await _db.Set<Appointment>()
            .IgnoreQueryFilters()
            .Where(a => a.AppointmentNumber.StartsWith(prefix))
            .MaxAsync(a => (string?)a.AppointmentNumber, ct);

        var sequence = last is null
            ? 1
            : int.Parse(last.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture) + 1;

        return $"{prefix}{sequence:D6}";
    }

    /// <summary>
    /// Works out what the caller may do next, from the state machine and the same rules the
    /// write paths enforce. A button a client shows because of this list will not be refused.
    /// </summary>
    private void DescribeActions(AppointmentDetailResponse detail)
    {
        var actions = new List<string>();
        var isOpen = detail.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
        var customerIsTooLate = IsCustomer && IsInsideCutoff(detail.ScheduledStartUtc);

        foreach (var next in AppointmentStateMachine.NextStatuses(detail.Status, _currentUser.Roles))
        {
            switch (next)
            {
                case AppointmentStatus.Confirmed:
                    actions.Add(AppointmentActions.Confirm);
                    break;
                case AppointmentStatus.InProgress:
                    actions.Add(AppointmentActions.Start);
                    break;
                case AppointmentStatus.Completed:
                    actions.Add(AppointmentActions.Complete);
                    break;
                case AppointmentStatus.Cancelled when !customerIsTooLate:
                    actions.Add(AppointmentActions.Cancel);
                    break;
                default:
                    break;
            }
        }

        if (isOpen && !customerIsTooLate)
        {
            actions.Add(AppointmentActions.Reschedule);
        }

        if (IsAdministrator && !AppointmentStateMachine.IsTerminal(detail.Status))
        {
            actions.Add(AppointmentActions.Assign);
        }

        if (IsCustomer && detail.Status == AppointmentStatus.Completed && !detail.HasReview)
        {
            actions.Add(AppointmentActions.LeaveReview);
        }

        detail.AllowedActions = actions;

        if (isOpen && customerIsTooLate)
        {
            detail.ChangeBlockedReason = CutoffMessage();
        }

        // Payment opens once the work is done, and only for the customer who owes it.
        detail.CanBePaid = IsCustomer
                           && detail.Status == AppointmentStatus.Completed
                           && detail.PaymentStatus is AppointmentPaymentStatus.Unpaid or AppointmentPaymentStatus.PendingPayment;

        if (!detail.CanBePaid)
        {
            detail.PaymentBlockedReason = detail.PaymentStatus switch
            {
                AppointmentPaymentStatus.Paid => "Rezervacija je već plaćena.",
                AppointmentPaymentStatus.Refunded or AppointmentPaymentStatus.PartiallyRefunded =>
                    "Za ovu rezervaciju je izvršen povrat novca.",
                _ when detail.Status == AppointmentStatus.Cancelled => "Rezervacija je otkazana.",
                _ when detail.Status != AppointmentStatus.Completed => "Plaćanje je moguće nakon završetka servisa.",
                _ => "Plaćanje vrši klijent kroz mobilnu aplikaciju.",
            };
        }

        if (IsCustomer)
        {
            detail.InternalNotes = null;
        }
    }

    private static string Display(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Na čekanju",
        AppointmentStatus.Confirmed => "Potvrđeno",
        AppointmentStatus.InProgress => "U toku",
        AppointmentStatus.Completed => "Završeno",
        AppointmentStatus.Cancelled => "Otkazano",
        _ => status.ToString(),
    };

    private static string Format(DateTime value) => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static NotFoundException NotFound() => new("Rezervacija nije pronađena.");
}
