namespace SmartGarage.Application.Abstractions;

public sealed record PasswordResetCodeMessage(
    string Email,
    string FirstName,
    string Code,
    DateTime ExpiresAtUtc);

/// <summary>
/// Delivers a password reset code to its owner. The API never sends e-mail itself, so that an
/// SMTP round trip cannot hold up a request; what sits behind this interface hands the message
/// on.
/// </summary>
public interface IPasswordResetCodeSender
{
    Task SendAsync(PasswordResetCodeMessage message, CancellationToken cancellationToken);
}
