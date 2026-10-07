namespace SmartGarage.Domain.Enums;

/// <summary>
/// Notification categories. The instructions require notifications for every relevant event,
/// not only one, which is why this list is deliberately complete.
/// </summary>
public enum NotificationType
{
    AppointmentCreated = 1,
    AppointmentConfirmed = 2,
    AppointmentRescheduled = 3,
    AppointmentCancelled = 4,
    AppointmentCompleted = 5,
    AppointmentReminder = 6,
    PaymentSucceeded = 7,
    PaymentFailed = 8,
    RefundIssued = 9,
    ServiceReminder = 10,
    ReviewRequested = 11,
    AccountStatusChanged = 12,
    System = 13,
}
