namespace SmartGarage.Contracts.Events;

/// <summary>
/// The Worker has stored a notification. The API consumes this and pushes it to the user over
/// SignalR if they are connected. The row already exists by the time this is published, so a
/// user who is offline still finds the notification the next time they open the inbox.
/// </summary>
public sealed record NotificationCreatedEvent : IntegrationEvent
{
    public required int NotificationId { get; init; }

    public required int UserId { get; init; }

    public required string Type { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public string? RelatedEntityType { get; init; }

    public int? RelatedEntityId { get; init; }
}
