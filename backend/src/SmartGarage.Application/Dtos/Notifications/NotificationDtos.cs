using SmartGarage.Application.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Dtos.Notifications;

public sealed class NotificationResponse
{
    public int Id { get; init; }

    public NotificationType Type { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public bool IsRead { get; init; }

    public DateTime? ReadAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>What the notification is about, so a tap can open the right screen.</summary>
    public string? RelatedEntityType { get; init; }

    public int? RelatedEntityId { get; init; }
}

public sealed class NotificationSearchRequest : BaseSearchRequest
{
    public bool? IsRead { get; init; }

    public NotificationType? Type { get; init; }
}

public sealed record UnreadCountResponse(int Count);
