using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Performance;

/// <summary>
/// A list page must cost the same number of SQL commands whether it holds one row or fifty:
/// related data is projected in the same query, never loaded row by row.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class QueryCountTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public QueryCountTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListPages_DoNotQueryOncePerRow()
    {
        var admin = (await _client.LoginAsync("admin")).AccessToken;
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(customer.AccessToken);
        var services = await _client.GetServicesForAsync(vehicle.EngineTypeId, customer.AccessToken);
        await _client.CreateVehicleAsync(customer.AccessToken);

        for (var i = 0; i < 3; i++)
        {
            await _client.BookAsync(customer.AccessToken, vehicle.Id, TestData.Slot(27 + i, 8), services[i].Id, services[i + 1].Id);
        }

        foreach (var (path, token) in new[]
        {
            ("/api/appointments", admin),
            ("/api/appointments", customer.AccessToken),
            ("/api/services", admin),
            ("/api/users", admin),
            ("/api/employees", admin),
            ("/api/payments", admin),
            ("/api/reviews", admin),
            ("/api/vehicles", customer.AccessToken),
            ("/api/notifications", customer.AccessToken),
        })
        {
            var (oneRowQueries, _) = await CountAsync($"{path}?pageSize=1", token);
            var (queries, rows) = await CountAsync($"{path}?pageSize=50", token);

            oneRowQueries.Should().BePositive("the counter has to see the API's commands");
            queries.Should().Be(oneRowQueries, $"{path} should not send more commands for a page of {rows} rows");
        }
    }

    private async Task<(int Queries, int Rows)> CountAsync(string url, string token)
    {
        // The first call warms the session cache, so both measured calls do the same work.
        (await _client.GetAsync(url, token)).EnsureSuccessStatusCode();

        _factory.Queries.Reset();
        var page = await (await _client.GetAsync(url, token)).ReadAsync<PagedResult<object>>();
        return (_factory.Queries.Count, page.Items.Count);
    }
}
