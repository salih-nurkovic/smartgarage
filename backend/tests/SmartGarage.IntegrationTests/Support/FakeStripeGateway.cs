using System.Collections.Concurrent;
using System.Text.Json;
using SmartGarage.Application.Abstractions;

namespace SmartGarage.IntegrationTests.Support;

/// <summary>
/// Stands in for Stripe so the payment flow can be tested without the network. A test decides
/// what Stripe "says" about an intent; the code under test has to find out the same way it does
/// in production - by asking.
/// </summary>
public sealed class FakeStripeGateway : IStripeGateway
{
    public const string ValidSignature = "t=1,v1=fake-signature";

    private readonly ConcurrentDictionary<string, StripeIntent> _intents = new();
    private readonly ConcurrentDictionary<string, long> _amounts = new();
    private readonly ConcurrentDictionary<string, string> _byIdempotencyKey = new();
    private readonly ConcurrentDictionary<string, StripeRefundResult> _refunds = new();
    private int _sequence;

    public string PublishableKey => "pk_test_fake";

    public Task<StripeIntent> CreatePaymentIntentAsync(
        long amountMinor,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        string idempotencyKey,
        CancellationToken ct)
    {
        // Like Stripe: the same idempotency key always returns the same intent.
        var id = _byIdempotencyKey.GetOrAdd(idempotencyKey, _ =>
        {
            var newId = $"pi_fake_{Interlocked.Increment(ref _sequence)}";
            _amounts[newId] = amountMinor;
            _intents[newId] = new StripeIntent(newId, $"{newId}_secret", "requires_payment_method", 0, null, null);
            return newId;
        });

        return Task.FromResult(_intents[id]);
    }

    public Task<StripeIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct) =>
        Task.FromResult(_intents[paymentIntentId]);

    public Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentId,
        long amountMinor,
        string idempotencyKey,
        CancellationToken ct) =>
        Task.FromResult(_refunds.GetOrAdd(
            idempotencyKey,
            _ => new StripeRefundResult($"re_fake_{Interlocked.Increment(ref _sequence)}", "succeeded")));

    public StripeWebhookEvent ParseWebhook(string payload, string signatureHeader)
    {
        if (signatureHeader != ValidSignature)
        {
            throw new InvalidOperationException("Signature does not match.");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        return new StripeWebhookEvent(
            root.GetProperty("id").GetString()!,
            root.GetProperty("type").GetString()!,
            root.GetProperty("data").GetProperty("object").GetProperty("id").GetString(),
            null,
            null);
    }

    /// <summary>The customer completed the payment sheet and the card was charged.</summary>
    public void Succeed(string paymentIntentId) =>
        _intents[paymentIntentId] = _intents[paymentIntentId] with
        {
            Status = "succeeded",
            AmountReceivedMinor = _amounts[paymentIntentId],
            LatestChargeId = $"ch_{paymentIntentId}",
            FailureMessage = null,
        };

    /// <summary>The card was declined; Stripe puts the intent back to waiting for a payment method.</summary>
    public void Decline(string paymentIntentId, string message) =>
        _intents[paymentIntentId] = _intents[paymentIntentId] with
        {
            Status = "requires_payment_method",
            FailureMessage = message,
        };

    public static string EventFor(string eventId, string type, string paymentIntentId) =>
        JsonSerializer.Serialize(new
        {
            id = eventId,
            type,
            data = new { @object = new { id = paymentIntentId, @object = "payment_intent" } },
        });
}
