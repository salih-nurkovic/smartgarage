using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// One Stripe payment attempt against an appointment.
/// <para>
/// <see cref="Status"/> is only ever written by the Stripe webhook handler or by explicit
/// server-side verification against Stripe. The client never marks a payment successful.
/// </para>
/// </summary>
public class Payment : BaseEntity
{
    /// <summary>Human-readable reference such as PAY-2026-000123, shown instead of the ID.</summary>
    public string PaymentNumber { get; set; } = string.Empty;

    public int AppointmentId { get; set; }
    public int PayerId { get; set; }

    /// <summary>
    /// Amount in the display currency, taken from the appointment's line snapshots. Server
    /// determined; the client sends only the appointment id.
    /// </summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// What Stripe actually charged, in the Stripe currency. Refunds are calculated from this
    /// value rather than from the catalogue price, as the instructions require
    /// ("refund se vrsi na osnovu stvarno naplacenog iznosa, a ne kalkulisane cijene").
    /// </summary>
    public decimal AmountChargedStripe { get; set; }

    public string StripeCurrency { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Created;

    /// <summary>Unique: one PaymentIntent can never produce two payment rows.</summary>
    public string StripePaymentIntentId { get; set; } = string.Empty;

    public string? StripeChargeId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Sent to Stripe so a retried request cannot create a second charge. Unique here too, so
    /// duplicate payment is prevented on both sides.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public Appointment Appointment { get; set; } = null!;
    public ApplicationUser Payer { get; set; } = null!;
    public ICollection<Refund> Refunds { get; set; } = [];
    public ICollection<PaymentEvent> Events { get; set; } = [];
}
