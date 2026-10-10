using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Infrastructure.Identity;

/// <summary>
/// Reads the authenticated user from the current HTTP request's claims.
/// <para>
/// Services inject this instead of touching <c>IHttpContextAccessor</c> or parsing tokens
/// themselves, which is what the instructions ask for: "IHttpContextAccessor treba injektovati
/// u servise umjesto manuelnog parsiranja tokena".
/// </para>
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public int? UserId =>
        int.TryParse(Principal?.FindFirstValue(AppClaims.UserId), out var id) ? id : null;

    public string? UserName => Principal?.FindFirstValue(AppClaims.UserName);

    public string? Email => Principal?.FindFirstValue(AppClaims.Email);

    public string? JwtId => Principal?.FindFirstValue(AppClaims.JwtId);

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(AppClaims.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public int RequireUserId() =>
        UserId ?? throw new InvalidOperationException(
            "No authenticated user on this request. This path is behind an authorization "
            + "policy, so reaching it anonymously means the policy is missing.");
}
