using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Dtos.Payments;
using SmartGarage.Application.Dtos.Reports;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Reports;

/// <summary>
/// Tests share one database, so each one measures a report before and after its own changes
/// and checks the difference rather than absolute figures.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ReportTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ReportTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RevenueReport_CountsWhatWasChargedLessRefunds_OnTheDayItHappened()
    {
        var admin = (await _client.LoginAsync("admin")).AccessToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var url = $"/api/reports/revenue?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}";

        var before = await (await _client.GetAsync(url, admin)).ReadAsync<RevenueReportResponse>();

        var (auth, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(auth.AccessToken);
        var services = await _client.GetServicesForAsync(vehicle.EngineTypeId, auth.AccessToken);
        var completed = await _client.BookAndCompleteAsync(_factory, auth.AccessToken, vehicle, TestData.Slot(25, 9), services[0].Id);
        var paymentId = await PayAsync(auth.AccessToken, completed.Id);

        var refund = decimal.Round(completed.TotalPrice / 4, 2);
        (await _client.SendJsonAsync(
                HttpMethod.Post,
                $"/api/payments/{paymentId}/refund",
                new RefundRequest { Amount = refund, Reason = "Djelimičan povrat za izvještaj." },
                admin))
            .EnsureSuccessStatusCode();

        var after = await (await _client.GetAsync(url, admin)).ReadAsync<RevenueReportResponse>();

        after.GrossRevenue.Value.Should().Be(before.GrossRevenue.Value + completed.TotalPrice);
        after.RefundedAmount.Should().Be(before.RefundedAmount + refund);
        after.NetRevenue.Value.Should().Be(after.GrossRevenue.Value - after.RefundedAmount);
        after.PaymentCount.Should().Be(before.PaymentCount + 1);
        after.Refunds.Should().Contain(r => r.Reason == "Djelimičan povrat za izvještaj." && r.Amount == refund);

        // Every section adds up to the summary.
        after.ByPeriod.Should().ContainSingle().Which.Revenue.Should().Be(after.NetRevenue.Value);
        after.ByCategory.Sum(c => c.Amount).Should().BeGreaterThanOrEqualTo(completed.TotalPrice);
        after.ByCategory.Sum(c => c.Percent).Should().BeApproximately(100m, 0.5m);
        after.GeneratedBy.Should().NotBeNullOrWhiteSpace();

        // The day before and the day after hold none of it.
        var yesterday = await (await _client.GetAsync(
                $"/api/reports/revenue?from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}", admin))
            .ReadAsync<RevenueReportResponse>();
        yesterday.Refunds.Should().NotContain(r => r.Reason == "Djelimičan povrat za izvještaj.");
        var tomorrow = await (await _client.GetAsync(
                $"/api/reports/revenue?from={today.AddDays(1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}", admin))
            .ReadAsync<RevenueReportResponse>();
        tomorrow.GrossRevenue.Value.Should().Be(0);
    }

    [Fact]
    public async Task AppointmentReport_IncludesBothEndsOfTheRange_AndHonoursItsFilters()
    {
        var admin = (await _client.LoginAsync("admin")).AccessToken;
        var day = DateOnly.FromDateTime(TestData.Slot(26, 9));
        static string Url(DateOnly from, DateOnly to, string filter = "") =>
            $"/api/reports/appointments?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}{filter}";

        var before = await (await _client.GetAsync(Url(day, day), admin)).ReadAsync<AppointmentReportResponse>();
        var cancelledBefore = await (await _client.GetAsync(Url(day, day, "&status=Cancelled"), admin))
            .ReadAsync<AppointmentReportResponse>();

        var (auth, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(auth.AccessToken);
        var services = await _client.GetServicesForAsync(vehicle.EngineTypeId, auth.AccessToken);
        await _client.BookAsync(auth.AccessToken, vehicle.Id, TestData.Slot(26, 9), services[0].Id);
        var dropped = await _client.BookAsync(auth.AccessToken, vehicle.Id, TestData.Slot(26, 11), services[0].Id);
        (await _client.SendJsonAsync(
                HttpMethod.Post,
                $"/api/appointments/{dropped.Id}/cancel",
                new AppointmentCancelRequest { Reason = "Razlog za izvještaj." },
                admin))
            .EnsureSuccessStatusCode();

        // A single day is a range whose first and last day are the same.
        var after = await (await _client.GetAsync(Url(day, day), admin)).ReadAsync<AppointmentReportResponse>();
        after.Total.Value.Should().Be(before.Total.Value + 2);
        after.Cancelled.Should().Be(before.Cancelled + 1);
        after.ByStatus.Sum(s => s.Count).Should().Be((int)after.Total.Value);
        after.ByPeriod.Should().ContainSingle().Which.Appointments.Should().Be((int)after.Total.Value);
        after.CancellationReasons.Should().Contain(r => r.Reason == "Razlog za izvještaj.");
        after.Appointments.Should().BeEmpty("only the PDF lists the appointments themselves");

        var cancelled = await (await _client.GetAsync(Url(day, day, "&status=Cancelled"), admin))
            .ReadAsync<AppointmentReportResponse>();
        cancelled.Total.Value.Should().Be(cancelledBefore.Total.Value + 1);
        cancelled.ByStatus.Should().OnlyContain(s => s.Status == AppointmentStatus.Cancelled);

        (await (await _client.GetAsync(Url(day.AddDays(1), day.AddDays(7)), admin)).ReadAsync<AppointmentReportResponse>())
            .CancellationReasons.Should().NotContain(r => r.Reason == "Razlog za izvještaj.");
        (await (await _client.GetAsync(Url(day.AddDays(-7), day.AddDays(-1)), admin)).ReadAsync<AppointmentReportResponse>())
            .CancellationReasons.Should().NotContain(r => r.Reason == "Razlog za izvještaj.");
    }

    [Fact]
    public async Task EmptyRange_GivesZeroesAndAPdf_AndAnInvertedRangeIsRejected()
    {
        var admin = (await _client.LoginAsync("admin")).AccessToken;
        const string range = "from=2001-02-01&to=2001-02-28";

        var revenue = await (await _client.GetAsync($"/api/reports/revenue?{range}", admin)).ReadAsync<RevenueReportResponse>();
        revenue.GrossRevenue.Value.Should().Be(0);
        revenue.GrossRevenue.ChangePercent.Should().BeNull("there is nothing to compare with");
        revenue.AverageTransaction.Value.Should().Be(0);
        revenue.ByPeriod.Should().HaveCount(28).And.OnlyContain(p => p.Revenue == 0 && p.Appointments == 0);

        var appointments = await (await _client.GetAsync($"/api/reports/appointments?{range}", admin))
            .ReadAsync<AppointmentReportResponse>();
        appointments.Total.Value.Should().Be(0);
        appointments.CompletionRate.Should().Be(0);
        appointments.AverageRating.Should().BeNull();

        foreach (var path in new[] { "revenue", "appointments" })
        {
            var pdf = await _client.GetAsync($"/api/reports/{path}/pdf?{range}", admin);
            pdf.StatusCode.Should().Be(HttpStatusCode.OK);
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            pdf.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
            pdf.Content.Headers.ContentDisposition.FileName.Should().Contain("2001-02-01-2001-02-28");
            (await pdf.Content.ReadAsByteArrayAsync()).Take(5).Should().Equal("%PDF-"u8.ToArray());
        }

        var inverted = await _client.GetAsync("/api/reports/revenue?from=2026-03-10&to=2026-03-01", admin);
        inverted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await inverted.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("to");

        (await _client.GetAsync("/api/reports/revenue?from=2020-01-01&to=2026-01-01", admin))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reports_AreForAdministratorsOnly()
    {
        var employee = (await _client.LoginAsync("employee")).AccessToken;
        var (customer, _) = await _client.RegisterNewCustomerAsync();

        foreach (var path in new[] { "revenue", "revenue/pdf", "appointments", "appointments/pdf" })
        {
            (await _client.GetAsync($"/api/reports/{path}", employee)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _client.GetAsync($"/api/reports/{path}", customer.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _client.GetAsync($"/api/reports/{path}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    private async Task<int> PayAsync(string token, int appointmentId)
    {
        var intent = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/payments/intent", new PaymentIntentRequest { AppointmentId = appointmentId }, token))
            .ReadAsync<PaymentIntentResponse>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();
            _factory.Stripe.Succeed(await db.Payments
                .Where(p => p.Id == intent.PaymentId)
                .Select(p => p.StripePaymentIntentId)
                .SingleAsync());
        }

        (await _client.GetAsync($"/api/payments/{intent.PaymentId}", token)).EnsureSuccessStatusCode();
        return intent.PaymentId;
    }
}
