namespace SmartGarage.Application.Options;

/// <summary>The garage's working day, from <c>.env</c>. A new employee starts on these hours.</summary>
public sealed class BusinessHoursOptions
{
    public TimeOnly WorkdayStart { get; set; }

    public TimeOnly WorkdayEnd { get; set; }
}
