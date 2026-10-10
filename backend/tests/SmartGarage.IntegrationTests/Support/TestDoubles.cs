using SmartGarage.Application.Abstractions;
using SmartGarage.Contracts.Events;

namespace SmartGarage.IntegrationTests.Support;

/// <summary>A clock a test can move forward, so expiry can be tested without waiting.</summary>
public sealed class TestClock : IDateTimeProvider
{
    public TimeSpan Offset { get; set; } = TimeSpan.Zero;

    public DateTime UtcNow => DateTime.UtcNow + Offset;

    public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
}

/// <summary>
/// Stands where the broker would be and keeps what was published, so the tests need no
/// RabbitMQ. The reset code exists in readable form only in the published message, so this is
/// the one way a test can learn it - the same way the Worker does.
/// </summary>
public sealed class CapturingEventPublisher : IEventPublisher
{
    private readonly List<IntegrationEvent> _published = [];

    public IReadOnlyList<IntegrationEvent> Published => _published;

    public Task PublishAsync<TEvent>(TEvent message, string routingKey, CancellationToken ct)
        where TEvent : IntegrationEvent
    {
        _published.Add(message);
        return Task.CompletedTask;
    }

    public string LatestResetCodeFor(string email) =>
        _published
            .OfType<PasswordResetRequestedEvent>()
            .Last(m => string.Equals(m.Email, email, StringComparison.OrdinalIgnoreCase))
            .Code;
}