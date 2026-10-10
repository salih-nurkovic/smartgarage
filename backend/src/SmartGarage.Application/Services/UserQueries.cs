using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

/// <summary>Queries about users that more than one service needs, written once.</summary>
internal static class UserQueries
{
    /// <summary>
    /// Projects users to the response shape in SQL, role names included, so a page of users is
    /// one query rather than one per row.
    /// </summary>
    public static IQueryable<UserResponse> SelectResponse(
        this IQueryable<ApplicationUser> users,
        IAppDbContext db)
    {
        var userRoles = db.UserRoles;
        var roles = db.Roles;

        return users.Select(u => new UserResponse
        {
            Id = u.Id,
            UserName = u.UserName!,
            Email = u.Email!,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber!,
            HasProfileImage = u.ProfileImagePath != null,
            IsActive = u.IsActive,
            Roles = userRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                .ToList(),
            CreatedAtUtc = u.CreatedAtUtc,
            LastLoginAtUtc = u.LastLoginAtUtc,
        });
    }

    /// <summary>
    /// Rejects a username or e-mail that another account already holds, with the message under
    /// the right field.
    /// <para>
    /// Archived accounts are included for usernames on purpose: the unique index on usernames
    /// covers them too, so skipping them here would turn a clear message into a database error.
    /// </para>
    /// </summary>
    public static async Task EnsureUniqueAsync(
        this IAppDbContext db,
        string? normalizedUserName,
        string normalizedEmail,
        int? exceptUserId,
        CancellationToken ct)
    {
        var clashes = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id != exceptUserId
                        && (u.NormalizedUserName == normalizedUserName
                            || (u.NormalizedEmail == normalizedEmail && !u.IsDeleted)))
            .Select(u => new { u.NormalizedUserName, u.NormalizedEmail })
            .ToListAsync(ct);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (clashes.Any(c => c.NormalizedUserName == normalizedUserName))
        {
            errors["userName"] = ["Korisničko ime je već zauzeto. Izaberite drugo."];
        }

        if (clashes.Any(c => c.NormalizedEmail == normalizedEmail))
        {
            errors["email"] = ["Nalog sa ovom e-mail adresom već postoji."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
