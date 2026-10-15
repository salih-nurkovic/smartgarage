using SmartGarage.Application.Dtos.Notifications;

namespace SmartGarage.Application.Abstractions;

/// <summary>
/// Pushes to clients that are connected right now. Delivery is best effort by nature: whoever
/// is offline reads the same data from the REST endpoints later.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotificationReceivedAsync(int userId, NotificationResponse notification, CancellationToken ct);
}
