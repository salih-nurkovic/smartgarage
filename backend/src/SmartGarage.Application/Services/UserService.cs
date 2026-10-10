using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

/// <summary>User administration. Every method here sits behind the administrator policy.</summary>
public interface IUserService
{
    Task<PagedResult<UserResponse>> GetAsync(UserSearchRequest search, CancellationToken ct);

    Task<UserResponse> GetByIdAsync(int id, CancellationToken ct);

    Task<UserResponse> CreateAsync(UserCreateRequest request, CancellationToken ct);

    Task<UserResponse> UpdateAsync(int id, UserUpdateRequest request, CancellationToken ct);

    Task<UserResponse> SetStatusAsync(int id, UserStatusRequest request, CancellationToken ct);
}

public sealed class UserService : IUserService
{
    private const string EmployeeRoleMessage =
        "Uloga zaposlenika se dodjeljuje i uklanja kroz modul Zaposlenici, jer zaposlenik "
        + "ima i radni profil sa rasporedom.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppDbContext _db;
    private readonly ISessionService _sessions;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        IAppDbContext db,
        ISessionService sessions,
        ICurrentUserService currentUser,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _db = db;
        _sessions = sessions;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PagedResult<UserResponse>> GetAsync(UserSearchRequest search, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();

            query = query.Where(u =>
                u.FirstName.Contains(term)
                || u.LastName.Contains(term)
                || u.UserName!.Contains(term)
                || u.Email!.Contains(term));
        }

        if (search.IsActive is { } isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(search.Role))
        {
            var usersInRole = _db.UserRoles
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .Where(x => x.Name == search.Role)
                .Select(x => x.UserId);

            query = query.Where(u => usersInRole.Contains(u.Id));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await ApplySort(query, search)
            .Skip(search.Skip)
            .Take(search.ClampedPageSize)
            .SelectResponse(_db)
            .ToListAsync(ct);

        return new PagedResult<UserResponse>(
            items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<UserResponse> GetByIdAsync(int id, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .SelectResponse(_db)
            .SingleOrDefaultAsync(ct)
        ?? throw UserNotFound();

    public async Task<UserResponse> CreateAsync(UserCreateRequest request, CancellationToken ct)
    {
        if (request.Role == Roles.Employee)
        {
            throw new ValidationException("role", EmployeeRoleMessage);
        }

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

        await _db.ExecuteInTransactionAsync(
            async _ =>
            {
                (await _userManager.CreateAsync(user, request.Password)).ThrowIfFailed();
                (await _userManager.AddToRoleAsync(user, request.Role)).ThrowIfFailed();
            },
            ct);

        _logger.LogInformation(
            "Administrator {AdminId} created user {UserName} with role {Role}.",
            _currentUser.UserId, user.UserName, request.Role);

        return UserResponse.From(user, [request.Role]);
    }

    public async Task<UserResponse> UpdateAsync(int id, UserUpdateRequest request, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw UserNotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var roleChanges = currentRoles.Count != 1 || currentRoles[0] != request.Role;

        if (roleChanges)
        {
            // An administrator demoting themselves could leave the system with nobody able
            // to undo it.
            if (id == _currentUser.RequireUserId())
            {
                throw new BusinessRuleException("Ne možete promijeniti vlastitu ulogu.");
            }

            if (request.Role == Roles.Employee || currentRoles.Contains(Roles.Employee))
            {
                throw new ValidationException("role", EmployeeRoleMessage);
            }
        }

        var email = request.Email.Trim();

        await _db.EnsureUniqueAsync(
            normalizedUserName: null, _userManager.NormalizeEmail(email), exceptUserId: id, ct);

        var passwordChanges = !string.IsNullOrEmpty(request.NewPassword);

        await _db.ExecuteInTransactionAsync(
            async token =>
            {
                user.Email = email;
                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.PhoneNumber = request.PhoneNumber.Trim();

                (await _userManager.UpdateAsync(user)).ThrowIfFailed();

                if (roleChanges)
                {
                    (await _userManager.RemoveFromRolesAsync(user, currentRoles)).ThrowIfFailed();
                    (await _userManager.AddToRoleAsync(user, request.Role)).ThrowIfFailed();
                }

                if (passwordChanges)
                {
                    // No old password is asked for: this is an administrator acting on someone
                    // else's account, which the instructions treat differently from a user
                    // changing their own.
                    var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                    (await _userManager.ResetPasswordAsync(user, identityToken, request.NewPassword!))
                        .ThrowIfFailed("newPassword");
                }

                // Tokens already issued still carry the old role, and sessions opened with the
                // old password should not outlive it.
                if (roleChanges || passwordChanges)
                {
                    await _sessions.RevokeAllForUserAsync(
                        user.Id,
                        passwordChanges
                            ? SecurityRules.RevocationReasons.PasswordChanged
                            : SecurityRules.RevocationReasons.RoleChanged,
                        exceptJwtId: null,
                        token);
                }
            },
            ct);

        return UserResponse.From(user, [request.Role]);
    }

    public async Task<UserResponse> SetStatusAsync(int id, UserStatusRequest request, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw UserNotFound();

        if (!request.IsActive && id == _currentUser.RequireUserId())
        {
            throw new BusinessRuleException("Ne možete deaktivirati vlastiti nalog.");
        }

        if (user.IsActive != request.IsActive)
        {
            user.IsActive = request.IsActive;

            await _db.ExecuteInTransactionAsync(
                async token =>
                {
                    await _db.SaveChangesAsync(token);

                    // Without this a deactivated user would keep working until their access
                    // token expired.
                    if (!request.IsActive)
                    {
                        await _sessions.RevokeAllForUserAsync(
                            user.Id,
                            SecurityRules.RevocationReasons.AccountDeactivated,
                            exceptJwtId: null,
                            token);
                    }
                },
                ct);

            _logger.LogInformation(
                "Administrator {AdminId} set user {UserId} active={IsActive}. Reason: {Reason}",
                _currentUser.UserId, user.Id, request.IsActive, request.Reason);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return UserResponse.From(user, roles);
    }

    // Newest first unless asked otherwise, so a record that was just added is at the top of
    // the list, as the instructions expect.
    private static IQueryable<ApplicationUser> ApplySort(
        IQueryable<ApplicationUser> query,
        UserSearchRequest search) =>
        search.OrderBy?.ToUpperInvariant() switch
        {
            "USERNAME" => Order(query, u => u.UserName, search.Descending),
            "LASTNAME" => Order(query, u => u.LastName, search.Descending).ThenBy(u => u.FirstName),
            "EMAIL" => Order(query, u => u.Email, search.Descending),
            "LASTLOGINATUTC" => Order(query, u => u.LastLoginAtUtc, search.Descending),
            _ => query.OrderByDescending(u => u.CreatedAtUtc).ThenByDescending(u => u.Id),
        };

    private static IOrderedQueryable<ApplicationUser> Order<TKey>(
        IQueryable<ApplicationUser> query,
        Expression<Func<ApplicationUser, TKey>> key,
        bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    private static NotFoundException UserNotFound() => new("Korisnik nije pronađen.");
}
