using SmartGarage.Domain.Entities;

namespace SmartGarage.Application.Abstractions;

/// <summary>An issued access token together with the facts the session record needs.</summary>
public sealed record AccessToken(string Value, string JwtId, DateTime ExpiresAtUtc);

/// <summary>Issues signed access tokens. Validation is done by the JWT bearer middleware.</summary>
public interface IJwtTokenService
{
    AccessToken CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles);
}
