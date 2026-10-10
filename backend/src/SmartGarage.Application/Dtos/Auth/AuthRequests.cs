namespace SmartGarage.Application.Dtos.Auth;

/// <summary>
/// Self-registration. There is deliberately no role here: the instructions forbid the register
/// endpoint from accepting one, and a property that does not exist cannot be set by a client.
/// </summary>
public sealed class RegisterRequest
{
    public string UserName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    public string UserNameOrEmail { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string UserNameOrEmail { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmNewPassword { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;

    public string ConfirmNewPassword { get; init; } = string.Empty;
}
