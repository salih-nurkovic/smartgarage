using SmartGarage.Application.Dtos.Users;

namespace SmartGarage.Application.Dtos.Auth;

/// <summary>What a client receives after registering, logging in or refreshing.</summary>
public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTime AccessTokenExpiresAtUtc { get; init; }

    /// <summary>Returned once and never stored on the server in this form.</summary>
    public required string RefreshToken { get; init; }

    public required DateTime RefreshTokenExpiresAtUtc { get; init; }

    public required UserResponse User { get; init; }
}
