using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Notifications;
using SmartGarage.Domain.Constants;

namespace SmartGarage.API.Hubs;

/// <summary>
/// The live channel to connected clients.
/// <para>
/// It only pushes. There is nothing a client can invoke here, so every write in the system
/// still goes through an authorized REST endpoint and the hub adds no second way in. A
/// connection is accepted only with a valid token, and each one is tied to the user id inside
/// that token - a client cannot subscribe to somebody else's notifications.
/// </para>
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    /// <summary>Path the hub is mapped at. The JWT handler accepts a query-string token only under it.</summary>
    public const string Path = "/hubs/notifications";

    /// <summary>Name of the client method that receives a new notification.</summary>
    public const string NotificationReceived = "NotificationReceived";

    public static string RoleGroup(string role) => $"role:{role}";

    // Staff join a group per role, so events meant for "whoever is at the desk" reach every
    // administrator or employee who is online.
    public override async Task OnConnectedAsync()
    {
        foreach (var role in Context.User!.FindAll(AppClaims.Role).Select(c => c.Value))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));
        }

        await base.OnConnectedAsync();
    }
}

/// <summary>Maps a connection to the user id in its token, which makes <c>Clients.User(id)</c> work.</summary>
public sealed class JwtUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirstValue(AppClaims.UserId);
}

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task NotificationReceivedAsync(int userId, NotificationResponse notification, CancellationToken ct) =>
        _hub.Clients
            .User(userId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .SendAsync(NotificationHub.NotificationReceived, notification, ct);
}
