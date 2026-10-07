namespace SmartGarage.Domain.Enums;

/// <summary>Payment state of a single Stripe payment attempt.</summary>
public enum PaymentStatus
{
    /// <summary>PaymentIntent created, customer has not paid yet.</summary>
    Created = 1,

    /// <summary>Stripe needs an extra step from the customer, for example 3D Secure.</summary>
    RequiresAction = 2,

    /// <summary>Stripe is settling the charge.</summary>
    Processing = 3,

    /// <summary>Confirmed by the Stripe webhook or by server-side verification. Never by the client.</summary>
    Succeeded = 4,

    /// <summary>Declined or failed.</summary>
    Failed = 5,

    /// <summary>Abandoned before completion.</summary>
    Cancelled = 6,
}
