using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IAvailabilityService
{
    Task<IReadOnlyList<SlotResponse>> GetSlotsAsync(SlotSearchRequest request, CancellationToken ct);
}

/// <summary>
/// The slots of one day, each marked free or taken, for the booking screen.
/// <para>
/// It asks <see cref="ISlotPlanner"/> the same questions a booking is checked with, so a slot
/// shown as free here is one the server will accept. The whole day is answered with two
/// queries however many slots it has.
/// </para>
/// </summary>
public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IAppDbContext _db;
    private readonly ISlotPlanner _planner;
    private readonly BookingOptions _booking;

    public AvailabilityService(IAppDbContext db, ISlotPlanner planner, IOptions<BookingOptions> booking)
    {
        _db = db;
        _planner = planner;
        _booking = booking.Value;
    }

    public async Task<IReadOnlyList<SlotResponse>> GetSlotsAsync(SlotSearchRequest request, CancellationToken ct)
    {
        var duration = await GetDurationAsync(request.ServiceIds, ct);
        var workingDays = await _planner.LoadWorkingDaysAsync(ct);

        var windows = _planner.SlotStartsFor(request.Date)
            .Select(start => (Start: start, End: _planner.EndOf(start, duration, workingDays)))
            .ToList();

        if (windows.Count == 0)
        {
            return [];
        }

        var booked = await _planner.LoadBookedAsync(
            windows[0].Start, windows.Max(w => w.End), exceptAppointmentId: null, ct);

        return windows
            .Select(window =>
            {
                var state = _planner.Evaluate(window.Start, window.End, workingDays, booked);

                return new SlotResponse
                {
                    StartUtc = window.Start,
                    EndUtc = window.End,
                    IsAvailable = state.IsAvailable,
                    FreePlaces = state.FreePlaces,
                    UnavailableReason = state.UnavailableReason,
                };
            })
            .ToList();
    }

    // Before any service is chosen the screen can still show which hours are open at all, so
    // an empty selection is treated as a job of one slot.
    private async Task<int> GetDurationAsync(IReadOnlyList<int> serviceIds, CancellationToken ct)
    {
        var ids = serviceIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return _booking.SlotMinutes;
        }

        var durations = await _db.Set<Service>()
            .Where(s => ids.Contains(s.Id) && s.IsActive)
            .Select(s => s.EstimatedDurationMinutes)
            .ToListAsync(ct);

        return durations.Count == ids.Count
            ? durations.Sum()
            : throw new ValidationException("serviceIds", "Jedna ili više izabranih usluga više nije dostupna.");
    }
}
