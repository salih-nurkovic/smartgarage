using SmartGarage.Application.Abstractions;

namespace SmartGarage.Infrastructure.Identity;

/// <summary>
/// The real clock. Always UTC - Appendix A.4 of the instructions names mixing local and UTC
/// time as a defect, and <c>DateTime.Now</c> is banned at build time.
/// </summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
