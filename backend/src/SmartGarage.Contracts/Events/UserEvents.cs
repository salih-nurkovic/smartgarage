namespace SmartGarage.Contracts.Events;

/// <summary>A customer registered. The Worker answers with a welcome e-mail and a first notification.</summary>
public sealed record UserRegisteredEvent : IntegrationEvent
{
    public required int UserId { get; init; }

    public required string Email { get; init; }

    public required string FirstName { get; init; }

    public required string UserName { get; init; }
}

/// <summary>
/// Someone asked to reset a password. This message is the only place the code exists in
/// readable form: the database keeps a hash, and the Worker puts the code in the e-mail.
/// </summary>
public sealed record PasswordResetRequestedEvent : IntegrationEvent
{
    public required string Email { get; init; }

    public required string FirstName { get; init; }

    public required string Code { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }
}
