namespace SmartGarage.Contracts.Routing;

/// <summary>
/// Names shared by the publisher and the consumers. Declared once, in the one project both
/// sides reference, so a queue can never be bound to a key nobody publishes under.
/// </summary>
public static class MessageRouting
{
    public static class RoutingKeys
    {
        public const string UserRegistered = "user.registered";
        public const string PasswordResetRequested = "user.password-reset";
        public const string NotificationCreated = "notification.created";
        public const string AppointmentCreated = "appointment.created";
        public const string AppointmentStatusChanged = "appointment.status-changed";
        public const string AppointmentRescheduled = "appointment.rescheduled";
        public const string AppointmentReminderDue = "appointment.reminder-due";
        public const string ServiceReminderDue = "service.reminder-due";
        public const string PaymentSucceeded = "payment.succeeded";
        public const string PaymentFailed = "payment.failed";
        public const string RefundIssued = "payment.refunded";
    }

    public static class Queues
    {
        /// <summary>Everything that ends in an e-mail being sent.</summary>
        public const string Email = "smartgarage.email";

        /// <summary>Events that become a row in a user's notification inbox.</summary>
        public const string Notifications = "smartgarage.notifications";

        /// <summary>Stored notifications on their way to connected clients. Consumed by the API.</summary>
        public const string Realtime = "smartgarage.realtime";

        /// <summary>Messages that failed every retry, kept for an administrator to inspect.</summary>
        public const string DeadLetter = "smartgarage.dead-letter";
    }

    /// <summary>Where a queue sends a message it has given up on.</summary>
    public const string DeadLetterExchange = "smartgarage.dlx";

    /// <summary>
    /// Which routing keys feed which queue. One event can land in several queues: a
    /// registration is both an e-mail and a notification.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bindings =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Queues.Email] = [RoutingKeys.UserRegistered, RoutingKeys.PasswordResetRequested],
            [Queues.Notifications] =
            [
                RoutingKeys.UserRegistered,
                RoutingKeys.AppointmentCreated,
                RoutingKeys.AppointmentStatusChanged,
                RoutingKeys.AppointmentRescheduled,
                RoutingKeys.AppointmentReminderDue,
                RoutingKeys.ServiceReminderDue,
                RoutingKeys.PaymentSucceeded,
                RoutingKeys.PaymentFailed,
                RoutingKeys.RefundIssued,
            ],
            [Queues.Realtime] = [RoutingKeys.NotificationCreated],
        };
}
