using SmartGarage.Domain.Entities;

namespace SmartGarage.Application.Dtos.Users;

/// <summary>A user as clients see one. Never carries password, stamp or token fields.</summary>
public sealed class UserResponse
{
    public int Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>True when <c>GET /api/profile/image</c> has something to return.</summary>
    public bool HasProfileImage { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? LastLoginAtUtc { get; init; }

    public static UserResponse From(ApplicationUser user, IEnumerable<string> roles) => new()
    {
        Id = user.Id,
        UserName = user.UserName!,
        Email = user.Email!,
        FirstName = user.FirstName,
        LastName = user.LastName,
        PhoneNumber = user.PhoneNumber!,
        HasProfileImage = user.ProfileImagePath != null,
        IsActive = user.IsActive,
        Roles = [.. roles],
        CreatedAtUtc = user.CreatedAtUtc,
        LastLoginAtUtc = user.LastLoginAtUtc,
    };
}
