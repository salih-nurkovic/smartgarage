using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

/// <summary>
/// Counts the SQL commands the API sends, so a test can tell a query that stays the same size
/// from one that grows with the data (an N+1).
/// </summary>
public sealed class QueryCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
