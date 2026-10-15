using System.Net;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.API.Hubs;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Notifications;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Notifications;

[Collection(ApiTestGroup.Name)]
public sealed class NotificationTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Inbox_ShowsOnlyTheCallersNotifications_NewestFirst_WithAnUnreadCount()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();

        await SeedAsync(owner.User.Id, count: 5);
        await SeedAsync(stranger.User.Id, count: 2);

        var page = await GetPageAsync("/api/notifications?pageSize=3", owner.AccessToken);

        page.TotalCount.Should().Be(5);
        page.Items.Should().HaveCount(3);
        page.Items.Select(n => n.CreatedAtUtc).Should().BeInDescendingOrder();
        page.Items.Should().OnlyContain(n => !n.IsRead && n.Type == NotificationType.AppointmentConfirmed);

        (await GetPageAsync("/api/notifications?pageSize=100000", owner.AccessToken)).PageSize.Should().Be(100);
        (await UnreadAsync(owner.AccessToken)).Should().Be(5);
        (await UnreadAsync(stranger.AccessToken)).Should().Be(2);
    }

    [Fact]
    public async Task MarkingRead_ChangesOnlyTheCallersOwnNotification_AndKeepsTheFirstReadTime()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();

        var ids = await SeedAsync(owner.User.Id, count: 3);
        var url = $"/api/notifications/{ids[0]}/read";

        (await _client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, url).WithBearer(stranger.AccessToken)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var first = await (await _client.SendAsync(
                new HttpRequestMessage(HttpMethod.Patch, url).WithBearer(owner.AccessToken)))
            .ReadAsync<NotificationResponse>();

        first.IsRead.Should().BeTrue();
        first.ReadAtUtc.Should().NotBeNull();

        var second = await (await _client.SendAsync(
                new HttpRequestMessage(HttpMethod.Patch, url).WithBearer(owner.AccessToken)))
            .ReadAsync<NotificationResponse>();

        second.ReadAtUtc.Should().Be(first.ReadAtUtc);

        (await UnreadAsync(owner.AccessToken)).Should().Be(2);
        (await GetPageAsync("/api/notifications?isRead=true", owner.AccessToken))
            .Items.Should().ContainSingle().Which.Id.Should().Be(ids[0]);
    }

    [Fact]
    public async Task MarkAllRead_AndDelete_AffectOnlyTheCaller()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();

        var ids = await SeedAsync(owner.User.Id, count: 4);
        await SeedAsync(stranger.User.Id, count: 3);

        var all = await _client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "/api/notifications/read-all").WithBearer(owner.AccessToken));
        all.StatusCode.Should().Be(HttpStatusCode.OK);

        (await UnreadAsync(owner.AccessToken)).Should().Be(0);
        (await UnreadAsync(stranger.AccessToken)).Should().Be(3);

        (await _client.DeleteAsync($"/api/notifications/{ids[0]}", stranger.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/notifications/{ids[0]}", owner.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var remaining = await GetPageAsync("/api/notifications", owner.AccessToken);
        remaining.TotalCount.Should().Be(3);
        remaining.Items.Should().NotContain(n => n.Id == ids[0]);
    }

    [Fact]
    public async Task Hub_PushesANotificationOnlyToTheUserItIsFor()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();

        await using var ownerConnection = Connect(owner.AccessToken);
        await using var strangerConnection = Connect(stranger.AccessToken);

        var received = new TaskCompletionSource<NotificationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leaked = false;

        ownerConnection.On<NotificationResponse>(NotificationHub.NotificationReceived, n => received.TrySetResult(n));
        strangerConnection.On<NotificationResponse>(NotificationHub.NotificationReceived, _ => leaked = true);

        await ownerConnection.StartAsync();
        await strangerConnection.StartAsync();

        await _factory.Services.GetRequiredService<IRealtimeNotifier>().NotificationReceivedAsync(
            owner.User.Id,
            new NotificationResponse
            {
                Id = 1,
                Type = NotificationType.PaymentSucceeded,
                Title = "Plaćanje uspješno",
                Body = "Uplata je evidentirana.",
                CreatedAtUtc = DateTime.UtcNow,
            },
            CancellationToken.None);

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        pushed.Title.Should().Be("Plaćanje uspješno");
        pushed.Type.Should().Be(NotificationType.PaymentSucceeded);

        // Long polling delivers within one poll; a short wait is enough to catch a leak.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        leaked.Should().BeFalse();
    }

    [Fact]
    public async Task Hub_RefusesAConnectionWithoutAToken_AndATokenInTheUrlWorksOnlyForTheHub()
    {
        var customer = await _client.LoginAsync("customer");

        await using var anonymous = Connect(accessToken: null);
        var connect = async () => await anonymous.StartAsync();
        await connect.Should().ThrowAsync<HttpRequestException>();

        var negotiate = await _client.PostAsync(
            $"{NotificationHub.Path}/negotiate?negotiateVersion=1&access_token={customer.AccessToken}", content: null);
        negotiate.StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.GetAsync($"/api/profile?access_token={customer.AccessToken}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HubConnection Connect(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, NotificationHub.Path),
                options =>
                {
                    // The in-memory test server has no sockets, so the connection polls.
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;

                    if (accessToken is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    }
                })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

    private async Task<List<int>> SeedAsync(int userId, int count)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();
        var start = DateTime.UtcNow.AddHours(-count);

        var notifications = Enumerable.Range(0, count)
            .Select(i => new Notification
            {
                UserId = userId,
                Type = NotificationType.AppointmentConfirmed,
                Title = $"Rezervacija potvrđena {i}",
                Body = "Vaša rezervacija je potvrđena.",
                CreatedAtUtc = start.AddHours(i),
            })
            .ToList();

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();

        return notifications.OrderByDescending(n => n.CreatedAtUtc).Select(n => n.Id).ToList();
    }

    private async Task<int> UnreadAsync(string accessToken) =>
        (await (await _client.GetAsync("/api/notifications/unread-count", accessToken))
            .ReadAsync<UnreadCountResponse>()).Count;

    private async Task<PagedResult<NotificationResponse>> GetPageAsync(string url, string accessToken)
    {
        var response = await _client.GetAsync(url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<PagedResult<NotificationResponse>>();
    }
}
