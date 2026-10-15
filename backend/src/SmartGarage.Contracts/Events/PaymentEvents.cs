namespace SmartGarage.Contracts.Events;

/// <summary>Stripe confirmed a payment and the server recorded it.</summary>
public sealed record PaymentSucceededEvent : IntegrationEvent
{
    public required int PaymentId { get; init; }

    public required string PaymentNumber { get; init; }

    public required int AppointmentId { get; init; }

    public required string AppointmentNumber { get; init; }

    public required int PayerId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}

/// <summary>Stripe declined a payment.</summary>
public sealed record PaymentFailedEvent : IntegrationEvent
{
    public required int PaymentId { get; init; }

    public required int AppointmentId { get; init; }

    public required string AppointmentNumber { get; init; }

    public required int PayerId { get; init; }

    public string? Reason { get; init; }
}

/// <summary>Money was returned to the customer.</summary>
public sealed record RefundIssuedEvent : IntegrationEvent
{
    public required int PaymentId { get; init; }

    public required string PaymentNumber { get; init; }

    public required int AppointmentId { get; init; }

    public required int PayerId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Reason { get; init; }
}
