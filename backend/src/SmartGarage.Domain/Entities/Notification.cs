using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// An in-app notification. Carries read state, title, body and timestamp, which is exactly what
/// the instructions require of system notifications.
/// <para>
/// Rows are created by the Worker off the request thread, so booking an appointment stays fast,
/// and they exist in the database before they are pushed over SignalR - a client that was
/// offline still sees them on the next fetch.
/// </para>
/// </summary>
public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Lets a tap on the notification deep-link to the appointment or payment it concerns.</summary>
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    /// <summary>The mockups give every notification a delete action, so removal is soft.</summary>
    public bool IsDeleted { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
