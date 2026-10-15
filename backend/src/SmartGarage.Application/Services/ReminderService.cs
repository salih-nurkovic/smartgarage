using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Options;
using SmartGarage.Contracts.Events;
using SmartGarage.Contracts.Routing;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IReminderService
{
    /// <summary>Reminds customers of confirmed appointments that are coming up. Returns how many.</summary>
    Task<int> SendAppointmentRemindersAsync(CancellationToken ct);

    /// <summary>Tells owners which of their vehicles are now due for a service. Returns how many.</summary>
    Task<int> SendServiceRemindersAsync(CancellationToken ct);
}

/// <summary>
/// The scheduled scans behind reminders. The Worker's jobs call these on a timer; nothing in
/// the API does, and both refuse any caller that is not the system.
/// </summary>
public sealed class ReminderService : IReminderService
{
    // Bounds one run, so a backlog is worked off over several runs instead of in one burst.
    private const int BatchSize = 200;

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IEventPublisher _events;
    private readonly BookingOptions _booking;

    public ReminderService(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IEventPublisher events,
        IOptions<BookingOptions> booking)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _events = events;
        _booking = booking.Value;
    }

    public async Task<int> SendAppointmentRemindersAsync(CancellationToken ct)
    {
        EnsureSystem();

        var now = _clock.UtcNow;
        var until = now.AddHours(_booking.ReminderHoursBefore);
        var notifications = _db.Set<Notification>().IgnoreQueryFilters();

        // An appointment that already has a reminder notification is skipped, so running the
        // scan every hour reminds each customer exactly once.
        var due = await _db.Set<Appointment>()
            .Where(a => a.Status == AppointmentStatus.Confirmed
                        && a.ScheduledStartUtc > now
                        && a.ScheduledStartUtc <= until
                        && !notifications.Any(n =>
                            n.Type == NotificationType.AppointmentReminder
                            && n.RelatedEntityType == nameof(Appointment)
                            && n.RelatedEntityId == a.Id))
            .OrderBy(a => a.ScheduledStartUtc)
            .Take(BatchSize)
            .Select(a => new AppointmentReminderDueEvent
            {
                AppointmentId = a.Id,
                AppointmentNumber = a.AppointmentNumber,
                CustomerId = a.CustomerId,
                ScheduledStartUtc = a.ScheduledStartUtc,
            })
            .ToListAsync(ct);

        foreach (var reminder in due)
        {
            await _events.PublishAsync(reminder, MessageRouting.RoutingKeys.AppointmentReminderDue, ct);
        }

        return due.Count;
    }

    public async Task<int> SendServiceRemindersAsync(CancellationToken ct)
    {
        EnsureSystem();

        var now = _clock.UtcNow;

        // Due by the calendar or by the odometer, whichever the service defines and comes first.
        var due = await _db.Set<ServiceReminder>()
            .Include(r => r.Service)
            .Include(r => r.Vehicle).ThenInclude(v => v.Brand)
            .Include(r => r.Vehicle).ThenInclude(v => v.Model)
            .Where(r => r.Status == ServiceReminderStatus.Pending
                        && r.Vehicle.Owner.IsActive
                        && ((r.DueDateUtc != null && r.DueDateUtc <= now)
                            || (r.DueMileageKm != null && r.Vehicle.CurrentMileageKm >= r.DueMileageKm)))
            .OrderBy(r => r.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var reminder in due)
        {
            reminder.Status = ServiceReminderStatus.Sent;
            reminder.SentAtUtc = now;
        }

        // Marked as sent first: if publishing then fails the owner misses one notification,
        // which is better than receiving the same one on every run.
        await _db.SaveChangesAsync(ct);

        foreach (var reminder in due)
        {
            await _events.PublishAsync(
                new ServiceReminderDueEvent
                {
                    ReminderId = reminder.Id,
                    OwnerId = reminder.Vehicle.OwnerId,
                    VehicleId = reminder.VehicleId,
                    VehicleDisplay = $"{reminder.Vehicle.Brand.Name} {reminder.Vehicle.Model.Name} "
                                     + $"({reminder.Vehicle.RegistrationPlate})",
                    ServiceName = reminder.Service.Name,
                },
                MessageRouting.RoutingKeys.ServiceReminderDue,
                ct);
        }

        return due.Count;
    }

    private void EnsureSystem()
    {
        if (!_currentUser.IsInRole(Roles.System))
        {
            throw new ForbiddenException("Ovu radnju izvršava samo sistem.");
        }
    }
}
