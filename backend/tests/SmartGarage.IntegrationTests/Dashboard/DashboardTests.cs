using System.Net;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Dashboard;
using SmartGarage.Application.Dtos.ErrorLogs;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Dashboard;

[Collection(ApiTestGroup.Name)]
public sealed class DashboardTests
{
    private readonly HttpClient _client;

    public DashboardTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_ShowsRevenueToAdministrators_Only_AndIsClosedToCustomers()
    {
        var admin = await _client.LoginAsync("admin");
        var employee = await _client.LoginAsync("employee");
        var (customer, _) = await _client.RegisterNewCustomerAsync();

        var forAdmin = await (await _client.GetAsync("/api/dashboard", admin.AccessToken)).ReadAsync<DashboardResponse>();
        forAdmin.Customers.Value.Should().BeGreaterThan(0);
        forAdmin.MonthRevenue.Should().NotBeNull();
        forAdmin.RevenueByMonth.Should().HaveCount(6);

        var forEmployee = await (await _client.GetAsync("/api/dashboard", employee.AccessToken)).ReadAsync<DashboardResponse>();
        forEmployee.MonthRevenue.Should().BeNull();
        forEmployee.RevenueByMonth.Should().BeNull();
        forEmployee.Customers.Value.Should().Be(forAdmin.Customers.Value);

        (await _client.GetAsync("/api/dashboard", customer.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ErrorLog_IsReadableByAdministrators_WithTheStackTraceOnlyInTheDetail()
    {
        var admin = await _client.LoginAsync("admin");
        var employee = await _client.LoginAsync("employee");

        var page = await (await _client.GetAsync("/api/error-logs?pageSize=5", admin.AccessToken))
            .ReadAsync<PagedResult<ErrorLogListItemResponse>>();
        page.PageSize.Should().Be(5);

        (await _client.GetAsync("/api/error-logs", employee.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/error-logs/999999999", admin.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        if (page.Items.Count > 0)
        {
            var body = await (await _client.GetAsync($"/api/error-logs/{page.Items[0].Id}", admin.AccessToken))
                .Content.ReadAsStringAsync();
            body.Should().Contain("stackTrace");
        }
    }
}
