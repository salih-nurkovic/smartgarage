using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Dtos.Recommendations;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Recommendations;

[Collection(ApiTestGroup.Name)]
public sealed class RecommendationTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RecommendationTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Recommendations_AreForTheCustomersOwnVehicle_EachExplained_AndLogged()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();
        var employee = await _client.LoginAsync("employee");
        var vehicle = await _client.CreateVehicleAsync(customer.AccessToken);

        var response = await GetAsync(vehicle.Id, customer.AccessToken);

        response.Algorithm.Should().Be("Content-Based");
        response.Vehicle.VehicleId.Should().Be(vehicle.Id);
        response.Vehicle.MileageKm.Should().Be(vehicle.CurrentMileageKm);
        response.Vehicle.CompletedServiceCount.Should().Be(0);
        response.Items.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(5);
        response.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Explanation) && i.Factors.Count > 0);
        response.Items.Select(i => i.Rank).Should().BeInAscendingOrder();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();
            (await db.RecommendationLogs.CountAsync(l => l.VehicleId == vehicle.Id)).Should().Be(response.Items.Count);
        }

        (await _client.GetAsync($"/api/recommendations?vehicleId={vehicle.Id}", stranger.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.GetAsync($"/api/recommendations?vehicleId={vehicle.Id}", employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BrowsingAndSearchingTheCatalogue_AreRecordedAsSignals_ForCustomersOnly()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var admin = await _client.LoginAsync("admin");

        await _client.GetAsync("/api/services/1", customer.AccessToken);
        await _client.GetAsync("/api/services/1", admin.AccessToken);
        await _client.GetAsync("/api/services?search=zamjena", customer.AccessToken);
        await _client.GetAsync("/api/services", customer.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();
        var signals = await db.UserServiceSignals.Where(s => s.UserId == customer.User.Id).ToListAsync();

        signals.Should().ContainSingle(s => s.SignalType == SignalType.Viewed && s.ServiceId == 1);
        signals.Count(s => s.SignalType == SignalType.Searched).Should().BeInRange(1, 3);

        (await db.UserServiceSignals.CountAsync(s => s.UserId == admin.User.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Book_And_AlreadyDone_AreRecorded_AndDoneRestartsTheIntervalClock()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(customer.AccessToken);

        var before = await GetAsync(vehicle.Id, customer.AccessToken);
        var withInterval = before.Items.First(i => i.RecommendedMileageIntervalKm is not null || i.RecommendedMonthsInterval is not null);
        var clicked = before.Items.First(i => i.ServiceId != withInterval.ServiceId);

        (await _client.SendJsonAsync(
                HttpMethod.Post,
                $"/api/recommendations/{clicked.ServiceId}/clicked",
                new RecommendationClickRequest { VehicleId = vehicle.Id },
                customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _client.SendJsonAsync(
                HttpMethod.Post,
                $"/api/recommendations/{withInterval.ServiceId}/done",
                new RecommendationDoneRequest { VehicleId = vehicle.Id, PerformedAtUtc = DateTime.UtcNow.AddDays(1) },
                customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.SendJsonAsync(
                HttpMethod.Post,
                $"/api/recommendations/{withInterval.ServiceId}/done",
                new RecommendationDoneRequest { VehicleId = vehicle.Id },
                customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

            (await db.UserServiceSignals.SingleAsync(s =>
                    s.UserId == customer.User.Id && s.SignalType == SignalType.MarkedDone))
                .PerformedAtMileageKm.Should().Be(vehicle.CurrentMileageKm);

            (await db.RecommendationLogs
                    .Where(l => l.VehicleId == vehicle.Id && l.ServiceId == clicked.ServiceId)
                    .OrderByDescending(l => l.GeneratedAtUtc)
                    .FirstAsync())
                .WasClicked.Should().BeTrue();
        }

        // Just done: the service drops, and if it is still listed the reason is the penalty, not urgency.
        var after = await GetAsync(vehicle.Id, customer.AccessToken);
        var again = after.Items.SingleOrDefault(i => i.ServiceId == withInterval.ServiceId);

        if (again is not null)
        {
            again.Factors.Should().Contain(f => f.Code == "RECENTLY_DONE");
            again.Rank.Should().BeGreaterThan(withInterval.Rank);
        }
    }

    private async Task<RecommendationsResponse> GetAsync(int vehicleId, string token)
    {
        var response = await _client.GetAsync($"/api/recommendations?vehicleId={vehicleId}", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<RecommendationsResponse>();
    }
}
