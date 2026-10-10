using SmartGarage.Application.Abstractions;

namespace SmartGarage.IntegrationTests.Support;

/// <summary>A clock a test can move forward, so expiry can be tested without waiting.</summary>
public sealed class TestClock : IDateTimeProvider
{
    public TimeSpan Offset { get; set; } = TimeSpan.Zero;

    public DateTime UtcNow => DateTime.UtcNow + Offset;

    public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
}

/// <summary>
/// Stands where the e-mail would go and keeps what was "sent". The reset code exists in plain
/// form only in that message, so this is the one way a test can learn it - the same way a
/// real user does.
/// </summary>
public sealed class CapturingResetCodeSender : IPasswordResetCodeSender
{
    private readonly List<PasswordResetCodeMessage> _messages = [];

    public IReadOnlyList<PasswordResetCodeMessage> Messages => _messages;

    public Task SendAsync(PasswordResetCodeMessage message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public string LatestCodeFor(string email) =>
        _messages.Last(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase)).Code;
}
