using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Scheduling;

namespace SmartGarage.Application.Services;

public sealed record BookedWindow(DateTime StartUtc, DateTime EndUtc);

public sealed record SlotState(bool IsAvailable, int FreePlaces, string? UnavailableReason);

/// <summary>
/// The one definition of when the garage can take a vehicle.
/// <para>
/// The slot list a customer is shown and the check a booking must pass both come from here,
/// so what is offered is exactly what will be accepted. Availability is decided on the
/// server; a client that sent a time of its own choosing is checked just the same.
/// </para>
/// <para>
/// Working hours are kept in UTC and shown as they are - the garage's clock. The capacity
/// rule is deliberately on the safe side: an appointment counts against the limit for every
/// hour of its window.
/// </para>
/// </summary>
public interface ISlotPlanner
{
    IReadOnlyList<DateTime> SlotStartsFor(DateOnly day);

    bool IsSlotStart(DateTime startUtc);

    /// <summary>The weekdays on which at least one available mechanic works.</summary>
    Task<IReadOnlySet<DayOfWeek>> LoadWorkingDaysAsync(CancellationToken ct);

    Task<IReadOnlyList<BookedWindow>> LoadBookedAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int? exceptAppointmentId,
        CancellationToken ct);

    DateTime EndOf(DateTime startUtc, int durationMinutes, IReadOnlySet<DayOfWeek> workingDays);

    SlotState Evaluate(
        DateTime startUtc,
        DateTime endUtc,
        IReadOnlySet<DayOfWeek> workingDays,
        IReadOnlyList<BookedWindow> booked);
}

public sealed class SlotPlanner : ISlotPlanner
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly BookingOptions _booking;
    private readonly BusinessHoursOptions _hours;

    public SlotPlanner(
        IAppDbContext db,
        IDateTimeProvider clock,
        IOptions<BookingOptions> booking,
        IOptions<BusinessHoursOptions> hours)
    {
        _db = db;
        _clock = clock;
        _booking = booking.Value;
        _hours = hours.Value;
    }

    // A slot may start only if it also fits before closing time, which is what makes the last
    // bookable hour 16:00 for a garage that closes at 17:00.
    public IReadOnlyList<DateTime> SlotStartsFor(DateOnly day)
    {
        var starts = new List<DateTime>();
        var slot = TimeSpan.FromMinutes(_booking.SlotMinutes);
        var closes = day.ToDateTime(_hours.WorkdayEnd, DateTimeKind.Utc);

        for (var start = day.ToDateTime(_hours.WorkdayStart, DateTimeKind.Utc); start + slot <= closes; start += slot)
        {
            starts.Add(start);
        }

        return starts;
    }

    public bool IsSlotStart(DateTime startUtc) =>
        SlotStartsFor(DateOnly.FromDateTime(startUtc)).Contains(startUtc);

    public async Task<IReadOnlySet<DayOfWeek>> LoadWorkingDaysAsync(CancellationToken ct)
    {
        var days = await _db.Set<EmployeeSchedule>()
            .Where(s => s.IsWorkingDay
                        && s.EmployeeProfile.IsAvailableForAssignment
                        && s.EmployeeProfile.User.IsActive)
            .Select(s => s.DayOfWeek)
            .Distinct()
            .ToListAsync(ct);

        return days.ToHashSet();
    }

    public async Task<IReadOnlyList<BookedWindow>> LoadBookedAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int? exceptAppointmentId,
        CancellationToken ct) =>
        await _db.Set<Appointment>()
            .Where(a => a.Status != AppointmentStatus.Completed
                        && a.Status != AppointmentStatus.Cancelled
                        && a.Id != exceptAppointmentId
                        && a.ScheduledStartUtc < toUtc
                        && fromUtc < a.ScheduledEndUtc)
            .Select(a => new BookedWindow(a.ScheduledStartUtc, a.ScheduledEndUtc))
            .ToListAsync(ct);

    public DateTime EndOf(DateTime startUtc, int durationMinutes, IReadOnlySet<DayOfWeek> workingDays) =>
        WorkingTimeCalculator.GetEnd(
            startUtc, durationMinutes, _hours.WorkdayStart, _hours.WorkdayEnd, workingDays.Contains);

    public SlotState Evaluate(
        DateTime startUtc,
        DateTime endUtc,
        IReadOnlySet<DayOfWeek> workingDays,
        IReadOnlyList<BookedWindow> booked)
    {
        var now = _clock.UtcNow;

        if (startUtc <= now)
        {
            return Unavailable("Izabrani termin je već prošao.");
        }

        if (startUtc > now.AddDays(_booking.MaxDaysAhead))
        {
            return Unavailable($"Rezervacije se primaju najviše {_booking.MaxDaysAhead} dana unaprijed.");
        }

        if (!workingDays.Contains(startUtc.DayOfWeek))
        {
            return Unavailable("Servis ne radi izabranog dana.");
        }

        var taken = booked.Count(b => b.StartUtc < endUtc && startUtc < b.EndUtc);
        var free = _booking.MaxConcurrent - taken;

        return free > 0
            ? new SlotState(true, free, null)
            : Unavailable("Sva mjesta u ovom terminu su zauzeta. Izaberite drugi termin.");
    }

    private static SlotState Unavailable(string reason) => new(false, 0, reason);
}
