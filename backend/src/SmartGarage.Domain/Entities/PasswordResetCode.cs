using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A six-digit password reset code sent to a user by e-mail.
/// <para>
/// Appendix A.3 of the instructions requires reset codes to have a defined expiry and never to
/// be stored in plain text. The code is therefore kept only as a PBKDF2 hash, next to the
/// moment it stops being valid.
/// </para>
/// </summary>
public class PasswordResetCode : BaseEntity
{
    public int UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set when the code changes a password, so it can never be used twice.</summary>
    public DateTime? UsedAtUtc { get; set; }

    /// <summary>
    /// Six digits are only a million possibilities, so guessing is capped: after too many
    /// wrong attempts the code is dead even if it has not expired.
    /// </summary>
    public int FailedAttempts { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
}