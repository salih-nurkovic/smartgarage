using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Application.Options;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);

    Task LogoutAsync(CancellationToken ct);

    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);

    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly ISessionService _sessions;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly IPasswordResetCodeSender _codeSender;
    private readonly JwtOptions _jwt;
    private readonly PasswordResetOptions _reset;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IAppDbContext db,
        IJwtTokenService tokens,
        ISessionService sessions,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IPasswordHasher<ApplicationUser> hasher,
        IPasswordResetCodeSender codeSender,
        IOptions<JwtOptions> jwt,
        IOptions<PasswordResetOptions> reset,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tokens = tokens;
        _sessions = sessions;
        _currentUser = currentUser;
        _clock = clock;
        _hasher = hasher;
        _codeSender = codeSender;
        _jwt = jwt.Value;
        _reset = reset.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var userName = request.UserName.Trim();
        var email = request.Email.Trim();

        await _db.EnsureUniqueAsync(
            _userManager.NormalizeName(userName),
            _userManager.NormalizeEmail(email),
            exceptUserId: null,
            ct);

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            IsActive = true,
        };

        // The role is fixed here and nowhere else. Whatever a client puts in the request,
        // self-registration can only ever produce a customer.
        string[] roles = [Roles.Customer];

        var response = await _db.ExecuteInTransactionAsync(
            async token =>
            {
                (await _userManager.CreateAsync(user, request.Password)).ThrowIfFailed();
                (await _userManager.AddToRoleAsync(user, Roles.Customer)).ThrowIfFailed();

                var session = IssueSession(user, roles);
                await _db.SaveChangesAsync(token);
                return session;
            },
            ct);

        _logger.LogInformation("User {UserName} registered.", user.UserName);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(request.UserName.Trim());

        if (user is null)
        {
            throw InvalidCredentials();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(
                "Nalog je privremeno zaključan zbog previše neuspjelih pokušaja prijave. "
                + "Pokušajte ponovo kasnije.");
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning("Failed login attempt for {UserName}.", user.UserName);
            throw InvalidCredentials();
        }

        // Checked only after the password, so a stranger cannot learn which accounts exist
        // and are deactivated.
        if (!user.IsActive)
        {
            throw new ForbiddenException("Vaš nalog je deaktiviran. Obratite se administratoru.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        user.AccessFailedCount = 0;
        user.LastLoginAtUtc = _clock.UtcNow;

        var response = IssueSession(user, [.. roles]);
        await _db.SaveChangesAsync(ct);

        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = HashRefreshToken(request.RefreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            throw InvalidRefreshToken();
        }

        // A refresh token is single use. Seeing a rotated one again means two parties hold
        // it, and there is no telling which is the owner - so every session of that user ends.
        if (stored.RevokedReason == SecurityRules.RevocationReasons.Rotated)
        {
            await _sessions.RevokeAllForUserAsync(
                stored.UserId, SecurityRules.RevocationReasons.ReuseDetected, exceptJwtId: null, ct);

            _logger.LogWarning(
                "Rotated refresh token presented again for user {UserId}. All sessions revoked.",
                stored.UserId);

            throw InvalidRefreshToken();
        }

        var now = _clock.UtcNow;

        if (stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now || !stored.User.IsActive)
        {
            throw InvalidRefreshToken();
        }

        var roles = await _userManager.GetRolesAsync(stored.User);

        stored.RevokedAtUtc = now;
        stored.RevokedReason = SecurityRules.RevocationReasons.Rotated;

        var response = IssueSession(stored.User, [.. roles]);
        await _db.SaveChangesAsync(ct);

        // The access token issued with the old refresh token dies with it.
        _sessions.MarkRevoked(stored.JwtId);

        return response;
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var jwtId = _currentUser.JwtId
            ?? throw new UnauthorizedException("Sesija nije prepoznata. Prijavite se ponovo.");

        var now = _clock.UtcNow;

        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.JwtId == jwtId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.RevokedAtUtc, now)
                    .SetProperty(t => t.RevokedReason, SecurityRules.RevocationReasons.Logout),
                ct);

        _sessions.MarkRevoked(jwtId);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await FindByUserNameOrEmailAsync(request.UserNameOrEmail);

        // Returning normally whether or not the account exists is deliberate: the response
        // must not reveal which usernames and e-mail addresses are registered.
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for an unknown or inactive account.");
            return;
        }

        var now = _clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(_reset.CodeExpiryMinutes);
        var code = GenerateResetCode();

        var outstanding = await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.UsedAtUtc == null)
            .ToListAsync(ct);

        // Only the newest code may work, so an older e-mail cannot be used later.
        foreach (var previous in outstanding)
        {
            previous.UsedAtUtc = now;
        }

        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = _hasher.HashPassword(user, code),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now,
        });

        await _db.SaveChangesAsync(ct);

        await _codeSender.SendAsync(
            new PasswordResetCodeMessage(user.Email!, user.FirstName, code, expiresAtUtc), ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await FindByUserNameOrEmailAsync(request.UserNameOrEmail);

        if (user is null || !user.IsActive)
        {
            throw InvalidResetCode();
        }

        var now = _clock.UtcNow;

        var stored = await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.UsedAtUtc == null && c.ExpiresAtUtc > now)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (stored is null || stored.FailedAttempts >= SecurityRules.ResetCodeMaxFailedAttempts)
        {
            throw InvalidResetCode();
        }

        var verification = _hasher.VerifyHashedPassword(user, stored.CodeHash, request.Code);

        if (verification == PasswordVerificationResult.Failed)
        {
            stored.FailedAttempts++;
            await _db.SaveChangesAsync(ct);
            throw InvalidResetCode();
        }

        await _db.ExecuteInTransactionAsync(
            async token =>
            {
                stored.UsedAtUtc = now;
                user.AccessFailedCount = 0;
                user.LockoutEnd = null;

                var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                (await _userManager.ResetPasswordAsync(user, identityToken, request.NewPassword))
                    .ThrowIfFailed("newPassword");

                // Whoever knew the old password is signed out everywhere.
                await _sessions.RevokeAllForUserAsync(
                    user.Id, SecurityRules.RevocationReasons.PasswordReset, exceptJwtId: null, token);
            },
            ct);

        _logger.LogInformation("Password reset completed for user {UserId}.", user.Id);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("Sesija nije prepoznata. Prijavite se ponovo.");

        await _db.ExecuteInTransactionAsync(
            async token =>
            {
                (await _userManager.ChangePasswordAsync(
                        user, request.CurrentPassword, request.NewPassword))
                    .ThrowIfFailed("newPassword");

                // Other devices are signed out; the one that made the change stays in.
                await _sessions.RevokeAllForUserAsync(
                    user.Id,
                    SecurityRules.RevocationReasons.PasswordChanged,
                    _currentUser.JwtId,
                    token);
            },
            ct);
    }

    /// <summary>
    /// Creates the access token and the session row behind it. The row is added to the context
    /// but not saved, so the caller commits it together with whatever else it changed.
    /// </summary>
    private AuthResponse IssueSession(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var now = _clock.UtcNow;
        var accessToken = _tokens.CreateAccessToken(user, roles);
        var refreshToken = Base64Url.EncodeToString(
            RandomNumberGenerator.GetBytes(SecurityRules.RefreshTokenBytes));
        var refreshExpiresAtUtc = now.AddDays(_jwt.RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            User = user,
            TokenHash = HashRefreshToken(refreshToken),
            JwtId = accessToken.JwtId,
            ExpiresAtUtc = refreshExpiresAtUtc,
            CreatedAtUtc = now,
            CreatedByIp = _currentUser.IpAddress,
        });

        return new AuthResponse
        {
            AccessToken = accessToken.Value,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshExpiresAtUtc,
            User = UserResponse.From(user, roles),
        };
    }

    // Plain SHA-256 is right here, unlike for passwords: the input is 64 random bytes, so
    // there is nothing to guess and no need for a slow hash on every refresh.
    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private string GenerateResetCode()
    {
        var upperBound = (int)Math.Pow(10, _reset.CodeLength);

        return RandomNumberGenerator
            .GetInt32(upperBound)
            .ToString(new string('0', _reset.CodeLength), CultureInfo.InvariantCulture);
    }

    private Task<ApplicationUser?> FindByUserNameOrEmailAsync(string userNameOrEmail)
    {
        var key = userNameOrEmail.Trim();

        return key.Contains('@', StringComparison.Ordinal)
            ? _userManager.FindByEmailAsync(key)
            : _userManager.FindByNameAsync(key);
    }

    // One message for an unknown user and for a wrong password, so the response does not
    // confirm that a username exists.
    private static UnauthorizedException InvalidCredentials() =>
        new("Pogrešno korisničko ime ili lozinka.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new("Sesija je istekla. Prijavite se ponovo.");

    private static ValidationException InvalidResetCode() =>
        new("code", "Kod nije ispravan ili je istekao. Zatražite novi kod.");
}
