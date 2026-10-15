namespace SmartGarage.Application.Abstractions;

/// <summary>A PaymentIntent as Stripe reports it.</summary>
public sealed record StripeIntent(
    string Id,
    string ClientSecret,
    string Status,
    long AmountReceivedMinor,
    string? LatestChargeId,
    string? FailureMessage);

public sealed record StripeRefundResult(string Id, string Status);

/// <summary>The parts of a verified webhook event the payment flow needs.</summary>
public sealed record StripeWebhookEvent(
    string Id,
    string Type,
    string? PaymentIntentId,
    string? RefundId,
    string? RefundStatus);

/// <summary>
/// Stripe, as the service layer sees it. Amounts are in minor units (cents), which is what
/// Stripe expects and what keeps rounding out of the picture.
/// </summary>
public interface IStripeGateway
{
    /// <summary>Handed to the mobile app so it can open the payment sheet. It is public by design.</summary>
    string PublishableKey { get; }

    Task<StripeIntent> CreatePaymentIntentAsync(
        long amountMinor,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        string idempotencyKey,
        CancellationToken ct);

    Task<StripeIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct);

    Task<StripeRefundResult> CreateRefundAsync(
        string paymentIntentId,
        long amountMinor,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Verifies the <c>Stripe-Signature</c> header against the webhook secret and reads the
    /// event. Throws when the signature does not match, so an unsigned or forged request never
    /// gets as far as touching a payment.
    /// </summary>
    StripeWebhookEvent ParseWebhook(string payload, string signatureHeader);
}
