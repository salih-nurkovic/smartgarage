using SmartGarage.Application.Common;

namespace SmartGarage.Application.Dtos.Users;

public sealed class UserSearchRequest : BaseSearchRequest
{
    public string? Role { get; init; }

    public bool? IsActive { get; init; }
}

/// <summary>An administrator creating an account. Only here can a role be chosen.</summary>
public sealed class UserCreateRequest
{
    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}

/// <summary>
/// An administrator editing an account. The password fields are optional and the old password
/// is never asked for: empty means "leave the password alone", as the instructions require.
/// </summary>
public sealed class UserUpdateRequest
{
    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string? NewPassword { get; init; }

    public string? ConfirmNewPassword { get; init; }
}

public sealed class UserStatusRequest
{
    public bool IsActive { get; init; }

    /// <summary>Required when deactivating, so the audit log says why.</summary>
    public string? Reason { get; init; }
}

/// <summary>A user editing their own profile. Identity comes from the token, never from here.</summary>
public sealed class ProfileUpdateRequest
{
    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;
}
