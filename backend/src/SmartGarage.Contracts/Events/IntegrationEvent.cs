namespace SmartGarage.Contracts.Events;

/// <summary>
/// Base for every message the API publishes and the Worker consumes.
/// <para>
/// A message carries everything its consumer needs - recipient address, display name, the
/// values to print - so the Worker never has to call back into the API to do its job.
/// </para>
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Unique per message. A consumer can use it to recognise a redelivery.</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Ties a failure in the Worker back to the HTTP request that caused the message.</summary>
    public string? CorrelationId { get; init; }
}
