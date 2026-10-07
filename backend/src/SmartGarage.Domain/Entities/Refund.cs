using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A full or partial refund against a payment. Refunds accumulate: the remaining refundable
/// amount is the payment amount minus everything already pending or succeeded.
/// </summary>
public class Refund : BaseEntity
{
    public int PaymentId { get; set; }

    /// <summary>Never more than what is still refundable. Enforced inside a transaction.</summary>
    public decimal Amount { get; set; }

    /// <summary>Mandatory. The instructions require a reason on every reversal.</summary>
    public string Reason { get; set; } = string.Empty;

    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public string StripeRefundId { get; set; } = string.Empty;

    public int RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public Payment Payment { get; set; } = null!;
    public ApplicationUser RequestedByUser { get; set; } = null!;
}
