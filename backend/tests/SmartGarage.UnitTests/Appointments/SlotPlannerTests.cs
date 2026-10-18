using FluentAssertions;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Options;
using SmartGarage.Application.Services;
using Xunit;

namespace SmartGarage.UnitTests.Appointments;

/// <summary>
/// The slot list and the booking check share one definition of a free slot, so these rules
/// decide both what a customer is offered and what the server accepts.
/// </summary>
public sealed class SlotPlannerTests
{
    // 2026-10-19 is a Monday.
    private static readonly DateTime Monday = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Monday.AddDays(-3).AddHours(12);

    private static readonly IReadOnlySet<DayOfWeek> Weekdays = new HashSet<DayOfWeek>
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    };

    private readonly SlotPlanner _planner = new(
        null!,
        new FixedClock(Now),
        Options.Create(new BookingOptions { MaxConcurrent = 5, SlotMinutes = 60, MaxDaysAhead = 90 }),
        Options.Create(new BusinessHoursOptions { WorkdayStart = new TimeOnly(8, 0), WorkdayEnd = new TimeOnly(17, 0) }));

    [Fact]
    public void Slots_RunOnTheHour_FromOpeningUntilTheLastOneThatEndsByClosing()
    {
        var slots = _planner.SlotStartsFor(DateOnly.FromDateTime(Monday));

        slots.Should().HaveCount(9);
        slots[0].Should().Be(Monday.AddHours(8));
        slots[^1].Should().Be(Monday.AddHours(16));
    }

    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(16, 0, true)]
    [InlineData(16, 30, false)]
    [InlineData(17, 0, false)]
    [InlineData(7, 0, false)]
    public void OnlyTheOfferedStartsAreAccepted(int hour, int minute, bool expected) =>
        _planner.IsSlotStart(Monday.AddHours(hour).AddMinutes(minute)).Should().Be(expected);

    [Fact]
    public void FifthOverlappingBooking_TakesTheLastPlace_AndASixthIsRefused()
    {
        var start = Monday.AddHours(10);
        var end = start.AddHours(1);

        _planner.Evaluate(start, end, Weekdays, Booked(start, end, count: 4))
            .Should().Be(new SlotState(true, 1, null));

        var full = _planner.Evaluate(start, end, Weekdays, Booked(start, end, count: 5));
        full.IsAvailable.Should().BeFalse();
        full.UnavailableReason.Should().Contain("zauzeta");
    }

    [Fact]
    public void BookingsThatOnlyTouchTheWindow_DoNotTakeAPlace()
    {
        var start = Monday.AddHours(10);
        var end = start.AddHours(1);
        var before = Booked(start.AddHours(-1), start, count: 5);
        var after = Booked(end, end.AddHours(1), count: 5);

        _planner.Evaluate(start, end, Weekdays, [.. before, .. after]).FreePlaces.Should().Be(5);
    }

    [Fact]
    public void ALongBooking_CountsForEveryHourItSpans()
    {
        var longJob = Booked(Monday.AddHours(8), Monday.AddDays(1).AddHours(12), count: 5);

        _planner.Evaluate(Monday.AddDays(1).AddHours(11), Monday.AddDays(1).AddHours(12), Weekdays, longJob)
            .IsAvailable.Should().BeFalse();
        _planner.Evaluate(Monday.AddDays(1).AddHours(12), Monday.AddDays(1).AddHours(13), Weekdays, longJob)
            .IsAvailable.Should().BeTrue();
    }

    [Theory]
    [InlineData(-4, "prošao")]          // Thursday before "now"
    [InlineData(-2, "ne radi")]         // Saturday
    [InlineData(91, "90 dana")]         // past the booking horizon
    public void Refusals_SayWhy(int dayOffset, string reasonFragment)
    {
        var start = Monday.AddDays(dayOffset).AddHours(10);

        var state = _planner.Evaluate(start, start.AddHours(1), Weekdays, []);

        state.IsAvailable.Should().BeFalse();
        state.FreePlaces.Should().Be(0);
        state.UnavailableReason.Should().Contain(reasonFragment);
    }

    private static List<BookedWindow> Booked(DateTime start, DateTime end, int count) =>
        [.. Enumerable.Repeat(new BookedWindow(start, end), count)];

    private sealed class FixedClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow => now;

        public DateOnly TodayUtc => DateOnly.FromDateTime(now);
    }
}
