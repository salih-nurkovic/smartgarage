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
    }

    public static class Queues
    {
        /// <summary>Everything that ends in an e-mail being sent.</summary>
        public const string Email = "smartgarage.email";

        /// <summary>Messages that failed every retry, kept for an administrator to inspect.</summary>
        public const string DeadLetter = "smartgarage.dead-letter";
    }

    /// <summary>Where a queue sends a message it has given up on.</summary>
    public const string DeadLetterExchange = "smartgarage.dlx";

    /// <summary>Which routing keys feed the e-mail queue.</summary>
    public static readonly IReadOnlyList<string> EmailBindings =
    [
        RoutingKeys.UserRegistered,
        RoutingKeys.PasswordResetRequested,
    ];
}
