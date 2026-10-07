namespace SmartGarage.Domain.Enums;

/// <summary>Lifecycle of a maintenance reminder generated after a completed appointment.</summary>
public enum ServiceReminderStatus
{
    /// <summary>Scheduled, not yet due.</summary>
    Pending = 1,

    /// <summary>Due date or mileage reached and the customer has been notified.</summary>
    Sent = 2,

    /// <summary>Customer dismissed it.</summary>
    Dismissed = 3,

    /// <summary>The service was performed, so the reminder is satisfied.</summary>
    Fulfilled = 4,
}
