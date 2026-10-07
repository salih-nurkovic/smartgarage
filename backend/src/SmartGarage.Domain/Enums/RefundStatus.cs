namespace SmartGarage.Domain.Enums;

/// <summary>Lifecycle of a refund, mirrored from Stripe through the webhook.</summary>
public enum RefundStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
}
