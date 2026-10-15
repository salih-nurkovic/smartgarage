using FluentAssertions;
using SmartGarage.Domain.Scheduling;
using Xunit;

namespace SmartGarage.UnitTests.Appointments;

/// <summary>
/// The instructions name long durations (25 h, 48 h) as an edge case that has to be computed
/// correctly. A garage open 08:00-17:00 on weekdays gives nine working hours a day.
/// </summary>
public sealed class WorkingTimeCalculatorTests
{
    private static readonly TimeOnly Opens = new(8, 0);
    private static readonly TimeOnly Closes = new(17, 0);

    // 2026-10-19 is a Monday.
    private static readonly DateTime Monday = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(10, 60, 0, 11)]     // an hour's job ends an hour later
    [InlineData(8, 540, 0, 17)]     // exactly one full day ends at closing time, the same day
    [InlineData(8, 600, 1, 9)]      // ten hours: nine today, one tomorrow morning
    [InlineData(16, 120, 1, 9)]     // starts in the last hour, the rest carries over
    [InlineData(8, 1500, 2, 15)]    // 25 h = 9 + 9 + 7 -> Wednesday 15:00
    [InlineData(8, 2880, 7, 11)]    // 48 h = five full days + 3 h -> next Monday 11:00
    public void GetEnd_CountsOnlyWorkingHours(int startHour, int minutes, int expectedDayOffset, int expectedHour)
    {
        var end = WorkingTimeCalculator.GetEnd(Monday.AddHours(startHour), minutes, Opens, Closes, IsWeekday);

        end.Should().Be(Monday.AddDays(expectedDayOffset).AddHours(expectedHour));
    }

    [Fact]
    public void GetEnd_SkipsTheWeekend()
    {
        var fridayAfternoon = Monday.AddDays(4).AddHours(16);

        var end = WorkingTimeCalculator.GetEnd(fridayAfternoon, 120, Opens, Closes, IsWeekday);

        end.Should().Be(Monday.AddDays(7).AddHours(9));
        end.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void GetEnd_NeverEndsOutsideWorkingHours()
    {
        for (var minutes = 30; minutes <= 3000; minutes += 30)
        {
            var end = WorkingTimeCalculator.GetEnd(Monday.AddHours(8), minutes, Opens, Closes, IsWeekday);
            var time = TimeOnly.FromDateTime(end);

            IsWeekday(end.DayOfWeek).Should().BeTrue();
            time.Should().BeOnOrAfter(Opens).And.BeOnOrBefore(Closes);
        }
    }

    [Fact]
    public void GetEnd_RefusesAScheduleWithNoWorkingDay()
    {
        var act = () => WorkingTimeCalculator.GetEnd(Monday.AddHours(8), 60, Opens, Closes, _ => false);

        act.Should().Throw<InvalidOperationException>();
    }

    private static bool IsWeekday(DayOfWeek day) => day is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
}
