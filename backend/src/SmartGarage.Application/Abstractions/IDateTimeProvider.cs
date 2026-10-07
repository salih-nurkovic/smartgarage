namespace SmartGarage.Application.Abstractions;

/// <summary>
/// The clock, behind an interface so that time-dependent logic can be tested without waiting.
/// <para>
/// Everything in the system is UTC. Appendix A.4 of the instructions names mixing
/// <c>DateTime.Now</c> and <c>DateTime.UtcNow</c> as a defect, especially inside Docker, so
/// <c>DateTime.Now</c> is banned at build time and this is the only approved source of "now".
/// </para>
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateOnly TodayUtc { get; }
}
