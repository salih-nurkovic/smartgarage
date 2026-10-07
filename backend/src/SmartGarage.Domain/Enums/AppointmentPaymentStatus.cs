namespace SmartGarage.Domain.Enums;

/// <summary>
/// Payment state of the appointment as a whole, which is not the same as the state of one
/// payment attempt: an appointment may collect several failed attempts before one succeeds.
/// </summary>
public enum AppointmentPaymentStatus
{
    /// <summary>Nothing owed yet, or no attempt made.</summary>
    Unpaid = 1,

    /// <summary>A PaymentIntent is open and awaiting the customer.</summary>
    PendingPayment = 2,

    /// <summary>Paid in full and confirmed server-side.</summary>
    Paid = 3,

    /// <summary>Some of the amount has been refunded.</summary>
    PartiallyRefunded = 4,

    /// <summary>The whole amount has been refunded.</summary>
    Refunded = 5,
}
