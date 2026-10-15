using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Infrastructure.Payments;
using Stripe;
using Xunit;

namespace SmartGarage.IntegrationTests.Payments;

/// <summary>
/// The real gateway's webhook check, against events signed exactly the way Stripe signs them:
/// HMAC-SHA256 over "timestamp.payload" with the webhook secret. No network is involved.
/// </summary>
public sealed class StripeSignatureTests
{
    private const string Secret = "whsec_test_secret_for_signature_checks";

    private readonly StripeGateway _gateway = new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STRIPE_WEBHOOK_SECRET"] = Secret,
                ["STRIPE_SECRET_KEY"] = "sk_test_not_used",
                ["STRIPE_PUBLISHABLE_KEY"] = "pk_test_not_used",
            })
            .Build(),
        new ServiceCollection().AddHttpClient().BuildServiceProvider().GetRequiredService<IHttpClientFactory>());

    [Fact]
    public void ACorrectlySignedEvent_IsAccepted_AndItsPaymentIntentRead()
    {
        var payload = EventJson("evt_signed", "pi_123");

        var parsed = _gateway.ParseWebhook(payload, Sign(payload, Secret, DateTimeOffset.UtcNow));

        parsed.Id.Should().Be("evt_signed");
        parsed.Type.Should().Be("payment_intent.succeeded");
        parsed.PaymentIntentId.Should().Be("pi_123");
    }

    [Fact]
    public void AnEventSignedWithAnotherSecret_IsRejected()
    {
        var payload = EventJson("evt_forged", "pi_123");

        var act = () => _gateway.ParseWebhook(payload, Sign(payload, "whsec_someone_else", DateTimeOffset.UtcNow));

        act.Should().Throw<StripeException>();
    }

    [Fact]
    public void AnEventChangedAfterSigning_IsRejected()
    {
        var payload = EventJson("evt_original", "pi_123");
        var signature = Sign(payload, Secret, DateTimeOffset.UtcNow);

        var act = () => _gateway.ParseWebhook(payload.Replace("pi_123", "pi_999", StringComparison.Ordinal), signature);

        act.Should().Throw<StripeException>();
    }

    [Fact]
    public void AnOldEventReplayedLater_IsRejected()
    {
        var payload = EventJson("evt_replayed", "pi_123");

        var act = () => _gateway.ParseWebhook(payload, Sign(payload, Secret, DateTimeOffset.UtcNow.AddMinutes(-30)));

        act.Should().Throw<StripeException>();
    }

    private static string EventJson(string eventId, string paymentIntentId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "{{StripeConfiguration.ApiVersion}}",
          "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "livemode": false,
          "pending_webhooks": 1,
          "request": { "id": null, "idempotency_key": null },
          "type": "payment_intent.succeeded",
          "data": { "object": { "id": "{{paymentIntentId}}", "object": "payment_intent", "status": "succeeded" } }
        }
        """;

    private static string Sign(string payload, string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));

        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
