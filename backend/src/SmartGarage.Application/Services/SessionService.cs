using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Options;

namespace SmartGarage.Application.Services;

/// <summary>
/// Answers one question on every authenticated request: is the session this access token
/// belongs to still alive?
/// <para>
/// This is what makes logout, deactivation and password changes take effect on the server at
/// once, instead of when the token happens to expire. The instructions require exactly that:
/// "Logout mora invalidirati token na serveru".
/// </para>
/// </summary>
public interface ISessionService
{
    Task<bool> IsActiveAsync(string jwtId, CancellationToken ct);

    /// <summary>Records in the cache that a session is over. The caller has already saved that.</summary>
    void MarkRevoked(string jwtId);

    /// <summary>
    /// Ends every live session of a user, optionally sparing the one making the request.
    /// Runs as a single UPDATE and never calls <c>SaveChanges</c>, so it is safe to use from
    /// inside another service's transaction.
    /// </summary>
    Task RevokeAllForUserAsync(int userId, string reason, string? exceptJwtId, CancellationToken ct);
}

public sealed class SessionService : ISessionService
{
    // A live session is re-checked against the database this often. Revocations write to the
    // cache directly, so this only bounds how long a change made outside the API goes unseen.
    private static readonly TimeSpan ActiveCacheDuration = TimeSpan.FromSeconds(30);

    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeProvider _clock;
    private readonly TimeSpan _accessTokenLifetime;

    public SessionService(
        IAppDbContext db,
        IMemoryCache cache,
        IDateTimeProvider clock,
        IOptions<JwtOptions> jwt)
    {
        _db = db;
        _cache = cache;
        _clock = clock;
        _accessTokenLifetime = TimeSpan.FromMinutes(jwt.Value.AccessTokenMinutes);
    }

    public async Task<bool> IsActiveAsync(string jwtId, CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey(jwtId), out bool cached))
        {
            return cached;
        }

        var now = _clock.UtcNow;

        var active = await _db.RefreshTokens.AnyAsync(
            t => t.JwtId == jwtId
                 && t.RevokedAtUtc == null
                 && t.ExpiresAtUtc > now
                 && t.User.IsActive,
            ct);

        _cache.Set(CacheKey(jwtId), active, active ? ActiveCacheDuration : _accessTokenLifetime);
        return active;
    }

    // Kept for as long as an access token can live: after that the token is rejected for
    // having expired and the entry is no longer needed.
    public void MarkRevoked(string jwtId) =>
        _cache.Set(CacheKey(jwtId), false, _accessTokenLifetime);

    public async Task RevokeAllForUserAsync(
        int userId,
        string reason,
        string? exceptJwtId,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var live = _db.RefreshTokens.Where(
            t => t.UserId == userId && t.RevokedAtUtc == null && t.JwtId != exceptJwtId);

        var jwtIds = await live.Select(t => t.JwtId).ToListAsync(ct);

        if (jwtIds.Count == 0)
        {
            return;
        }

        await live.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.RevokedReason, reason),
            ct);

        foreach (var jwtId in jwtIds)
        {
            MarkRevoked(jwtId);
        }
    }

    private static string CacheKey(string jwtId) => $"session:{jwtId}";
}
