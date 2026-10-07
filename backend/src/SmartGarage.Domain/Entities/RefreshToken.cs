using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Server-side session record. The instructions require logout to invalidate the token on the
/// server rather than merely deleting it on the client, and a stateless JWT cannot do that on
/// its own, so sessions are tracked here.
/// </summary>
public class RefreshToken : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 of the token. The raw value is returned to the client once and never stored, so
    /// a database leak does not hand over working sessions.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The <c>jti</c> of the access token issued alongside this refresh token. Revoking the
    /// session puts this id on a denylist, which is what makes logout take effect immediately
    /// instead of when the access token expires.
    /// </summary>
    public string JwtId { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByIp { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
