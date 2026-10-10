namespace SmartGarage.Domain.Constants;

/// <summary>Fixed security parameters that are part of the design rather than configuration.</summary>
public static class SecurityRules
{
    /// <summary>Entropy of a refresh token before encoding.</summary>
    public const int RefreshTokenBytes = 64;

    /// <summary>Wrong guesses allowed against one reset code before it stops working.</summary>
    public const int ResetCodeMaxFailedAttempts = 5;

    public const int PasswordMaxLength = 100;

    public static class RevocationReasons
    {
        public const string Logout = "Logout";
        public const string Rotated = "Rotated";
        public const string ReuseDetected = "RefreshTokenReuseDetected";
        public const string PasswordChanged = "PasswordChanged";
        public const string PasswordReset = "PasswordReset";
        public const string AccountDeactivated = "AccountDeactivated";
        public const string RoleChanged = "RoleChanged";
    }
}
