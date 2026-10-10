namespace SmartGarage.Application.Options;

/// <summary>Shape and lifetime of the password reset code, from <c>.env</c>.</summary>
public sealed class PasswordResetOptions
{
    public int CodeLength { get; set; }

    public int CodeExpiryMinutes { get; set; }
}
