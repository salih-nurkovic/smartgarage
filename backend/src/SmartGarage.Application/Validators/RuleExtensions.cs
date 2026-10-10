using FluentValidation;
using Microsoft.AspNetCore.Identity;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

/// <summary>
/// Field rules shared by every validator, so the same field is never validated two different
/// ways. Each message states the expected format, as the instructions require.
/// </summary>
public static class RuleExtensions
{
    public static IRuleBuilderOptions<T, string> PersonName<T>(
        this IRuleBuilder<T, string> rule,
        string label) =>
        rule
            .NotEmpty().WithMessage($"{label} je obavezno polje.")
            .Length(ValidationRules.NameMinLength, ValidationRules.NameMaxLength)
            .WithMessage(
                $"{label} mora imati između {ValidationRules.NameMinLength} i "
                + $"{ValidationRules.NameMaxLength} znakova.");

    public static IRuleBuilderOptions<T, string> RequiredText<T>(
        this IRuleBuilder<T, string> rule,
        string label,
        int minLength,
        int maxLength) =>
        rule
            .NotEmpty().WithMessage($"{label} je obavezno polje.")
            .Must(value => value.Trim().Length >= minLength && value.Trim().Length <= maxLength)
            .WithMessage($"{label}: unesite između {minLength} i {maxLength} znakova.");

    public static IRuleBuilderOptions<T, int> Mileage<T>(this IRuleBuilder<T, int> rule) =>
        rule
            .InclusiveBetween(0, ValidationRules.MileageMaxKm)
            .WithMessage($"Kilometraža mora biti između 0 i {ValidationRules.MileageMaxKm} km.");

    public static IRuleBuilderOptions<T, string> UserNameFormat<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Korisničko ime je obavezno polje.")
            .Matches(ValidationRules.UserName())
            .WithMessage(
                "Korisničko ime mora imati 3 do 50 znakova i smije sadržavati samo slova, "
                + "brojeve, tačku, crticu i donju crtu.");

    public static IRuleBuilderOptions<T, string> EmailFormat<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("E-mail adresa je obavezno polje.")
            .MaximumLength(ValidationRules.EmailMaxLength)
            .WithMessage($"E-mail adresa može imati najviše {ValidationRules.EmailMaxLength} znakova.")
            .Matches(ValidationRules.Email())
            .WithMessage("Unesite ispravnu e-mail adresu u formatu ime@domena.com.");

    public static IRuleBuilderOptions<T, string> PhoneFormat<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Broj telefona je obavezno polje.")
            .Matches(ValidationRules.Phone())
            .WithMessage("Unesite ispravan broj telefona, npr. +387 61 123 456.");

    public static IRuleBuilderOptions<T, string> RoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Uloga je obavezno polje.")
            .Must(role => Roles.All.Contains(role))
            .WithMessage($"Uloga mora biti jedna od: {string.Join(", ", Roles.All)}.");

    /// <summary>
    /// Mirrors the Identity password policy, which itself comes from <c>.env</c>. Checking it
    /// here gives each broken rule its own precise message under the password field.
    /// </summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> rule,
        PasswordOptions policy) =>
        rule
            .NotEmpty().WithMessage("Lozinka je obavezno polje.")
            .MinimumLength(policy.RequiredLength)
            .WithMessage($"Lozinka mora imati najmanje {policy.RequiredLength} znakova.")
            .MaximumLength(SecurityRules.PasswordMaxLength)
            .WithMessage($"Lozinka može imati najviše {SecurityRules.PasswordMaxLength} znakova.")
            .Must(value => !policy.RequireDigit || value.Any(char.IsAsciiDigit))
            .WithMessage("Lozinka mora sadržavati najmanje jednu cifru (0-9).")
            .Must(value => !policy.RequireUppercase || value.Any(char.IsAsciiLetterUpper))
            .WithMessage("Lozinka mora sadržavati najmanje jedno veliko slovo (A-Z).")
            .Must(value => !policy.RequireLowercase || value.Any(char.IsAsciiLetterLower))
            .WithMessage("Lozinka mora sadržavati najmanje jedno malo slovo (a-z).")
            .Must(value => !policy.RequireNonAlphanumeric || value.Any(c => !char.IsAsciiLetterOrDigit(c)))
            .WithMessage("Lozinka mora sadržavati najmanje jedan specijalni znak, npr. ! ili #.");
}
