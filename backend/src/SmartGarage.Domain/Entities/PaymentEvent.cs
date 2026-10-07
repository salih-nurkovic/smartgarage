using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Ledger of Stripe webhook events we have already processed.
/// <para>
/// This table is how payment confirmation becomes provably idempotent. Stripe retries webhooks,
/// sometimes many times. The unique index on <see cref="StripeEventId"/> means the second
/// delivery of an event fails to insert, and the handler returns 200 without touching the
/// payment again. The instructions require exactly this: "confirm payment mora biti
/// idempotentan; ako je status vec completed, efekti se ne smiju ponovo izvrsavati".
/// </para>
/// </summary>
public class PaymentEvent : BaseEntity
{
    /// <summary>Stripe's own event id. Unique - this is the idempotency guard.</summary>
    public string StripeEventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;
    public int? PaymentId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? ProcessingResult { get; set; }

    /// <summary>Hash of the raw payload, for diagnosing a mismatch without storing card data.</summary>
    public string RawPayloadHash { get; set; } = string.Empty;

    public Payment? Payment { get; set; }
}
