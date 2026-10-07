namespace SmartGarage.Domain.Enums;

/// <summary>
/// Appointment lifecycle. Persisted as int. Allowed transitions live in
/// <c>AppointmentStateMachine</c>, never in a controller.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>Booked by the customer, waiting for the garage to confirm.</summary>
    Pending = 1,

    /// <summary>Confirmed by staff. The slot is now committed.</summary>
    Confirmed = 2,

    /// <summary>The vehicle is in the garage and work has started.</summary>
    InProgress = 3,

    /// <summary>Work finished. Terminal. Payment becomes possible here.</summary>
    Completed = 4,

    /// <summary>Cancelled by the customer or by staff, always with a reason. Terminal.</summary>
    Cancelled = 5,
}
