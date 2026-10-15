namespace SmartGarage.Domain.Scheduling;

/// <summary>
/// Works out when a job ends, counting only the hours the garage is open.
/// <para>
/// The instructions single out long durations (25 h, 48 h) as a case that must be computed
/// correctly. Adding the minutes to the start time would have a 25-hour job finish in the
/// middle of the night; here the remainder carries over to the next working day instead.
/// </para>
/// </summary>
public static class WorkingTimeCalculator
{
    // A week with no working day at all would otherwise loop forever.
    private const int MaxDaysToSearch = 366;

    public static DateTime GetEnd(
        DateTime startUtc,
        int workMinutes,
        TimeOnly dayStart,
        TimeOnly dayEnd,
        Func<DayOfWeek, bool> isWorkingDay)
    {
        ArgumentNullException.ThrowIfNull(isWorkingDay);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workMinutes);

        if (dayEnd <= dayStart)
        {
            throw new ArgumentException("The working day must end after it starts.", nameof(dayEnd));
        }

        var remaining = TimeSpan.FromMinutes(workMinutes);
        var day = DateOnly.FromDateTime(startUtc);
        var cursor = startUtc;

        for (var searched = 0; searched < MaxDaysToSearch; searched++)
        {
            var opens = day.ToDateTime(dayStart, DateTimeKind.Utc);
            var closes = day.ToDateTime(dayEnd, DateTimeKind.Utc);

            if (isWorkingDay(day.DayOfWeek) && cursor < closes)
            {
                var from = cursor > opens ? cursor : opens;
                var available = closes - from;

                if (remaining <= available)
                {
                    return from + remaining;
                }

                remaining -= available;
            }

            day = day.AddDays(1);
            cursor = day.ToDateTime(dayStart, DateTimeKind.Utc);
        }

        throw new InvalidOperationException("No working day was found to finish the job in.");
    }
}
