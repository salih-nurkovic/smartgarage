using SmartGarage.Application.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Dtos.Payments;

/// <summary>Which appointment to pay. The amount is never part of the request.</summary>
public sealed class PaymentIntentRequest
{
    public int AppointmentId { get; init; }
}

/// <summary>What the mobile app needs to open the Stripe payment sheet.</summary>
public sealed class PaymentIntentResponse
{
    public int PaymentId { get; init; }

    public string PaymentNumber { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string PublishableKey { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;
}

public sealed class RefundResponse
{
    public decimal Amount { get; init; }

    public string Reason { get; init; } = string.Empty;

    public RefundStatus Status { get; init; }

    public DateTime RequestedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }
}

public sealed class PaymentResponse
{
    public int Id { get; init; }

    /// <summary>The identifier people see, e.g. PAY-2026-000123. The database id never is.</summary>
    public string PaymentNumber { get; init; } = string.Empty;

    public int AppointmentId { get; init; }

    public string AppointmentNumber { get; init; } = string.Empty;

    public string PayerFullName { get; init; } = string.Empty;

    public IReadOnlyList<string> ServiceNames { get; init; } = [];

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public PaymentStatus Status { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? PaidAtUtc { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>What Stripe actually took. Refunds are limited by this, not by the price.</summary>
    public decimal AmountCharged { get; init; }

    public decimal RefundedAmount { get; init; }

    public decimal RefundableAmount => Status == PaymentStatus.Succeeded ? AmountCharged - RefundedAmount : 0;

    public IReadOnlyList<RefundResponse> Refunds { get; init; } = [];
}

public sealed class RefundRequest
{
    /// <summary>Leave empty to refund everything that has not been refunded yet.</summary>
    public decimal? Amount { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed class PaymentSearchRequest : BaseSearchRequest
{
    public PaymentStatus? Status { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

/// <summary>The four figures at the top of the payments screen.</summary>
public sealed class PaymentSummaryResponse
{
    /// <summary>Everything collected, less everything refunded.</summary>
    public decimal NetRevenue { get; init; }

    public int Succeeded { get; init; }

    public int InProgress { get; init; }

    public int Failed { get; init; }
}
