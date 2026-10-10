using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Infrastructure.Identity;

/// <summary>
/// Issues HS256-signed access tokens.
/// <para>
/// Every token carries a unique <c>jti</c>. That id is stored on the session row, which is how
/// the server can later refuse one specific token even though its signature is still valid.
/// </para>
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly IDateTimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var now = _clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.AccessTokenMinutes);
        var jwtId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(AppClaims.UserId, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(AppClaims.JwtId, jwtId),
            new(AppClaims.UserName, user.UserName!),
            new(AppClaims.Email, user.Email!),
            new(AppClaims.FirstName, user.FirstName),
            new(AppClaims.LastName, user.LastName),
        };

        claims.AddRange(roles.Select(role => new Claim(AppClaims.Role, role)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = _credentials,
        });

        return new AccessToken(token, jwtId, expiresAtUtc);
    }
}
