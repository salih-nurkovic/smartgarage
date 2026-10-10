namespace SmartGarage.Application.Options;

/// <summary>
/// JWT settings, read from <c>.env</c> once at startup and injected from then on. Nothing reads
/// the environment per request.
/// </summary>
public sealed class JwtOptions
{
    public const int MinimumKeyBytes = 32;

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; }

    public int RefreshTokenDays { get; set; }
}
