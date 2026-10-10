using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

public sealed class UserCreateRequestValidator : AbstractValidator<UserCreateRequest>
{
    public UserCreateRequestValidator(IOptions<IdentityOptions> identity)
    {
        RuleFor(x => x.UserName).UserNameFormat();
        RuleFor(x => x.Email).EmailFormat();
        RuleFor(x => x.FirstName).PersonName("Ime");
        RuleFor(x => x.LastName).PersonName("Prezime");
        RuleFor(x => x.PhoneNumber).PhoneFormat();
        RuleFor(x => x.Role).RoleName();
        RuleFor(x => x.Password).StrongPassword(identity.Value.Password);
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Lozinke se ne podudaraju.");
    }
}

public sealed class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequest>
{
    public UserUpdateRequestValidator(IOptions<IdentityOptions> identity)
    {
        RuleFor(x => x.Email).EmailFormat();
        RuleFor(x => x.FirstName).PersonName("Ime");
        RuleFor(x => x.LastName).PersonName("Prezime");
        RuleFor(x => x.PhoneNumber).PhoneFormat();
        RuleFor(x => x.Role).RoleName();

        // Editing a user must not demand a new password. The password rules apply only when
        // the administrator actually typed one.
        When(x => !string.IsNullOrEmpty(x.NewPassword), () =>
        {
            RuleFor(x => x.NewPassword!).StrongPassword(identity.Value.Password);
            RuleFor(x => x.ConfirmNewPassword)
                .Equal(x => x.NewPassword).WithMessage("Lozinke se ne podudaraju.");
        });
    }
}

public sealed class UserStatusRequestValidator : AbstractValidator<UserStatusRequest>
{
    public UserStatusRequestValidator()
    {
        When(x => !x.IsActive, () =>
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Navedite razlog deaktivacije naloga.")
                .Length(ValidationRules.ReasonMinLength, ValidationRules.ReasonMaxLength)
                .WithMessage(
                    $"Razlog mora imati između {ValidationRules.ReasonMinLength} i "
                    + $"{ValidationRules.ReasonMaxLength} znakova."));
    }
}

public sealed class ProfileUpdateRequestValidator : AbstractValidator<ProfileUpdateRequest>
{
    public ProfileUpdateRequestValidator()
    {
        RuleFor(x => x.Email).EmailFormat();
        RuleFor(x => x.FirstName).PersonName("Ime");
        RuleFor(x => x.LastName).PersonName("Prezime");
        RuleFor(x => x.PhoneNumber).PhoneFormat();
    }
}
