using Microsoft.AspNetCore.Mvc;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Notifications;
using SmartGarage.Application.Services;

namespace SmartGarage.API.Controllers;

/// <summary>The signed-in user's notification inbox. Whose inbox it is comes from the token.</summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> Get(
        [FromQuery] NotificationSearchRequest request,
        CancellationToken ct) =>
        Ok(await _notifications.GetAsync(request, ct));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken ct) =>
        Ok(await _notifications.GetUnreadCountAsync(ct));

    [HttpPatch("{id:int}/read")]
    public async Task<ActionResult<NotificationResponse>> MarkRead(int id, CancellationToken ct) =>
        Ok(await _notifications.MarkReadAsync(id, ct));

    [HttpPatch("read-all")]
    public async Task<ActionResult<UnreadCountResponse>> MarkAllRead(CancellationToken ct) =>
        Ok(await _notifications.MarkAllReadAsync(ct));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<MessageResponse>> Delete(int id, CancellationToken ct) =>
        Ok(await _notifications.DeleteAsync(id, ct));
}
