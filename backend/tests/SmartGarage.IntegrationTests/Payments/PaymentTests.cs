using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Dtos.Payments;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Contracts.Events;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Payments;

[Collection(ApiTestGroup.Name)]
public sealed class PaymentTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PaymentTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Intent_IsOnlyForCompletedWork_ChargesTheServerComputedAmount_AndIsNeverDuplicated()
    {
        var (token, vehicle, serviceId) = await NewCustomerAsync();
        var (stranger, _, _) = await NewCustomerAsync();

        var pending = await _client.BookAsync(token, vehicle.Id, TestData.Slot(14, 13), serviceId);
        (await IntentAsync(token, pending.Id)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var completed = await _client.BookAndCompleteAsync(_factory, token, vehicle, TestData.Slot(14, 9), serviceId);

        (await IntentAsync(stranger, completed.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // An amount in the request has nowhere to go.
        var first = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/payments/intent", new { appointmentId = completed.Id, amount = 0.5 }, token))
            .ReadAsync<PaymentIntentResponse>();

        first.Amount.Should().Be(completed.TotalPrice);
        first.Currency.Should().Be("EUR");
        first.PaymentNumber.Should().MatchRegex(@"^PAY-\d{4}-\d{6}$");
        first.ClientSecret.Should().NotBeNullOrWhiteSpace();
        first.PublishableKey.Should().StartWith("pk_test_");

        var second = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();
        second.PaymentId.Should().Be(first.PaymentId);
        second.ClientSecret.Should().Be(first.ClientSecret);

        var detail = await AppointmentAsync(completed.Id, token);
        detail.PaymentStatus.Should().Be(AppointmentPaymentStatus.PendingPayment);
        detail.CanBePaid.Should().BeTrue("a payment that was started but not finished can be resumed");
    }

    [Fact]
    public async Task Payment_IsMarkedPaidOnlyWhenStripeConfirmsIt_AndOnlyOnce()
    {
        var (token, vehicle, serviceId) = await NewCustomerAsync();
        var completed = await _client.BookAndCompleteAsync(_factory, token, vehicle, TestData.Slot(15, 9), serviceId);
        var intent = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();

        // The app may say the sheet closed happily; the server still asks Stripe, which has not charged yet.
        (await PaymentAsync(intent.PaymentId, token)).Status.Should().Be(PaymentStatus.Created);

        _factory.Stripe.Succeed(await IntentIdAsync(intent.PaymentId));

        var paid = await PaymentAsync(intent.PaymentId, token);
        paid.Status.Should().Be(PaymentStatus.Succeeded);
        paid.PaidAtUtc.Should().NotBeNull();
        paid.AmountCharged.Should().Be(completed.TotalPrice);
        paid.RefundableAmount.Should().Be(completed.TotalPrice);

        // Checking again changes nothing and announces nothing a second time.
        await PaymentAsync(intent.PaymentId, token);
        await PaymentAsync(intent.PaymentId, token);
        Published<PaymentSucceededEvent>(e => e.PaymentId == intent.PaymentId).Should().ContainSingle();

        var detail = await AppointmentAsync(completed.Id, token);
        detail.PaymentStatus.Should().Be(AppointmentPaymentStatus.Paid);
        detail.CanBePaid.Should().BeFalse();
        detail.PaymentBlockedReason.Should().Be("Rezervacija je već plaćena.");

        (await IntentAsync(token, completed.Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Webhook_MustBeSigned_AndProcessingTheSameEventTwiceChangesNothing()
    {
        var (token, vehicle, serviceId) = await NewCustomerAsync();
        var completed = await _client.BookAndCompleteAsync(_factory, token, vehicle, TestData.Slot(16, 9), serviceId);
        var intent = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();
        var intentId = await IntentIdAsync(intent.PaymentId);

        _factory.Stripe.Succeed(intentId);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = FakeStripeGateway.EventFor(eventId, "payment_intent.succeeded", intentId);

        (await PostWebhookAsync(payload, "t=1,v1=forged")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostWebhookAsync(payload, signature: null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Stripe retries deliveries; the second copy must be a harmless no-op.
        (await PostWebhookAsync(payload, FakeStripeGateway.ValidSignature)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostWebhookAsync(payload, FakeStripeGateway.ValidSignature)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

        (await db.PaymentEvents.CountAsync(e => e.StripeEventId == eventId)).Should().Be(1);
        (await db.PaymentEvents.SingleAsync(e => e.StripeEventId == eventId)).ProcessedAtUtc.Should().NotBeNull();
        (await db.Payments.SingleAsync(p => p.Id == intent.PaymentId)).Status.Should().Be(PaymentStatus.Succeeded);

        Published<PaymentSucceededEvent>(e => e.PaymentId == intent.PaymentId).Should().ContainSingle();
    }

    [Fact]
    public async Task DeclinedCard_LeavesTheAppointmentUnpaid_AndANewAttemptStartsAFreshPayment()
    {
        var (token, vehicle, serviceId) = await NewCustomerAsync();
        var completed = await _client.BookAndCompleteAsync(_factory, token, vehicle, TestData.Slot(17, 9), serviceId);
        var first = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();

        _factory.Stripe.Decline(await IntentIdAsync(first.PaymentId), "Kartica je odbijena.");

        var failed = await PaymentAsync(first.PaymentId, token);
        failed.Status.Should().Be(PaymentStatus.Failed);
        failed.FailureReason.Should().Be("Kartica je odbijena.");

        (await AppointmentAsync(completed.Id, token)).PaymentStatus.Should().Be(AppointmentPaymentStatus.Unpaid);
        Published<PaymentFailedEvent>(e => e.PaymentId == first.PaymentId).Should().ContainSingle();

        var retry = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();
        retry.PaymentId.Should().NotBe(first.PaymentId);
        retry.ClientSecret.Should().NotBe(first.ClientSecret);
    }

    [Fact]
    public async Task Refunds_AreForAdministrators_AndCannotExceedWhatWasActuallyCharged()
    {
        var (token, vehicle, serviceId) = await NewCustomerAsync();
        var admin = (await _client.LoginAsync("admin")).AccessToken;
        var completed = await _client.BookAndCompleteAsync(_factory, token, vehicle, TestData.Slot(18, 9), serviceId);
        var intent = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();
        var url = $"/api/payments/{intent.PaymentId}/refund";

        // Not paid yet: nothing to give back.
        (await RefundAsync(url, admin, 1m)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        _factory.Stripe.Succeed(await IntentIdAsync(intent.PaymentId));
        await PaymentAsync(intent.PaymentId, token);

        var total = completed.TotalPrice;
        var part = decimal.Round(total / 2, 2);

        (await RefundAsync(url, token, part)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await RefundAsync(url, admin, total + 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var noReason = await _client.SendJsonAsync(HttpMethod.Post, url, new RefundRequest { Amount = part }, admin);
        (await noReason.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("reason");

        var partial = await (await RefundAsync(url, admin, part)).ReadAsync<PaymentResponse>();
        partial.RefundedAmount.Should().Be(part);
        partial.RefundableAmount.Should().Be(total - part);
        partial.Refunds.Should().ContainSingle().Which.Status.Should().Be(RefundStatus.Succeeded);
        (await AppointmentAsync(completed.Id, token)).PaymentStatus.Should().Be(AppointmentPaymentStatus.PartiallyRefunded);

        (await RefundAsync(url, admin, total - part + 0.01m)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // No amount: everything that is left.
        var full = await (await RefundAsync(url, admin, amount: null)).ReadAsync<PaymentResponse>();
        full.RefundedAmount.Should().Be(total);
        full.RefundableAmount.Should().Be(0);
        (await AppointmentAsync(completed.Id, token)).PaymentStatus.Should().Be(AppointmentPaymentStatus.Refunded);

        (await RefundAsync(url, admin, amount: null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Published<RefundIssuedEvent>(e => e.PaymentId == intent.PaymentId).Should().HaveCount(2);
    }

    [Fact]
    public async Task Payments_AreListedOnlyToTheirPayer_WhileAdministratorsSeeAllAndTheTotals()
    {
        var (token, vehicle, serviceId) = await NewCustomerAsync();
        var (stranger, _, _) = await NewCustomerAsync();
        var admin = (await _client.LoginAsync("admin")).AccessToken;

        var completed = await _client.BookAndCompleteAsync(_factory, token, vehicle, TestData.Slot(19, 9), serviceId);
        var intent = await (await IntentAsync(token, completed.Id)).ReadAsync<PaymentIntentResponse>();

        var mine = await (await _client.GetAsync("/api/payments", token)).ReadAsync<PagedResult<PaymentResponse>>();
        mine.Items.Should().ContainSingle().Which.AppointmentNumber.Should().Be(completed.AppointmentNumber);

        (await (await _client.GetAsync("/api/payments", stranger)).ReadAsync<PagedResult<PaymentResponse>>())
            .Items.Should().BeEmpty();
        (await _client.GetAsync($"/api/payments/{intent.PaymentId}", stranger)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var found = await (await _client.GetAsync($"/api/payments?search={intent.PaymentNumber}", admin))
            .ReadAsync<PagedResult<PaymentResponse>>();
        found.Items.Should().ContainSingle().Which.Id.Should().Be(intent.PaymentId);

        (await _client.GetAsync("/api/payments/summary", token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await _client.GetAsync("/api/payments/summary", admin)).ReadAsync<PaymentSummaryResponse>())
            .InProgress.Should().BeGreaterThanOrEqualTo(1);
    }

    private async Task<(string Token, VehicleResponse Vehicle, int ServiceId)> NewCustomerAsync()
    {
        var (auth, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(auth.AccessToken);
        var services = await _client.GetServicesForAsync(vehicle.EngineTypeId, auth.AccessToken);

        return (auth.AccessToken, vehicle, services[0].Id);
    }

    private Task<HttpResponseMessage> IntentAsync(string token, int appointmentId) =>
        _client.SendJsonAsync(
            HttpMethod.Post, "/api/payments/intent", new PaymentIntentRequest { AppointmentId = appointmentId }, token);

    private Task<HttpResponseMessage> RefundAsync(string url, string token, decimal? amount) =>
        _client.SendJsonAsync(HttpMethod.Post, url, new RefundRequest { Amount = amount, Reason = "Dogovoreni povrat." }, token);

    private async Task<PaymentResponse> PaymentAsync(int paymentId, string token)
    {
        var response = await _client.GetAsync($"/api/payments/{paymentId}", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<PaymentResponse>();
    }

    private async Task<AppointmentDetailResponse> AppointmentAsync(int appointmentId, string token) =>
        await (await _client.GetAsync($"/api/appointments/{appointmentId}", token)).ReadAsync<AppointmentDetailResponse>();

    private async Task<string> IntentIdAsync(int paymentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

        return await db.Payments.Where(p => p.Id == paymentId).Select(p => p.StripePaymentIntentId).SingleAsync();
    }

    private Task<HttpResponseMessage> PostWebhookAsync(string payload, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };

        if (signature is not null)
        {
            request.Headers.Add("Stripe-Signature", signature);
        }

        return _client.SendAsync(request);
    }

    private IEnumerable<TEvent> Published<TEvent>(Func<TEvent, bool> match) =>
        _factory.Events.Published.OfType<TEvent>().Where(match);
}
