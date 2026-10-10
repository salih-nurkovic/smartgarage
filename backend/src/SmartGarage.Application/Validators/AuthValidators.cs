using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Options;

namespace SmartGarage.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IOptions<IdentityOptions> identity)
    {
        RuleFor(x => x.UserName).UserNameFormat();
        RuleFor(x => x.Email).EmailFormat();
        RuleFor(x => x.FirstName).PersonName("Ime");
        RuleFor(x => x.LastName).PersonName("Prezime");
        RuleFor(x => x.PhoneNumber).PhoneFormat();
        RuleFor(x => x.Password).StrongPassword(identity.Value.Password);
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Lozinke se ne podudaraju.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Only presence is checked. Describing the username format or the password policy here
        // would tell an attacker which guesses are not worth making.
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Unesite korisničko ime.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Unesite lozinku.");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token je obavezan.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.UserNameOrEmail)
            .NotEmpty().WithMessage("Unesite korisničko ime ili e-mail adresu.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator(
        IOptions<IdentityOptions> identity,
        IOptions<PasswordResetOptions> reset)
    {
        var codeLength = reset.Value.CodeLength;

        RuleFor(x => x.UserNameOrEmail)
            .NotEmpty().WithMessage("Unesite korisničko ime ili e-mail adresu.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Unesite kod koji ste dobili e-mailom.")
            .Must(code => code.Length == codeLength && code.All(char.IsAsciiDigit))
            .WithMessage($"Kod se sastoji od tačno {codeLength} cifara.");
        RuleFor(x => x.NewPassword).StrongPassword(identity.Value.Password);
        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Lozinke se ne podudaraju.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator(IOptions<IdentityOptions> identity)
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Unesite trenutnu lozinku.");
        RuleFor(x => x.NewPassword)
            .StrongPassword(identity.Value.Password)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Nova lozinka mora biti različita od trenutne.");
        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Lozinke se ne podudaraju.");
    }
}
