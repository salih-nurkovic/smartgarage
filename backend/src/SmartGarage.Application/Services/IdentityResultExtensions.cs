using Microsoft.AspNetCore.Identity;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

/// <summary>
/// Turns a failed <see cref="IdentityResult"/> into the same field-keyed validation error
/// every other failure produces, with Bosnian messages instead of Identity's English ones.
/// </summary>
internal static class IdentityResultExtensions
{
    private const string GeneralField = "general";

    public static void ThrowIfFailed(this IdentityResult result, string passwordField = "password")
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = result.Errors
            .Select(error => Map(error.Code, passwordField))
            .GroupBy(e => e.Field, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Message).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        throw new ValidationException(errors);
    }

    private static (string Field, string Message) Map(string code, string passwordField) => code switch
    {
        nameof(IdentityErrorDescriber.DuplicateUserName) =>
            ("userName", "Korisničko ime je već zauzeto. Izaberite drugo."),
        nameof(IdentityErrorDescriber.InvalidUserName) =>
            ("userName", "Korisničko ime sadrži nedozvoljene znakove."),
        nameof(IdentityErrorDescriber.DuplicateEmail) =>
            ("email", "Nalog sa ovom e-mail adresom već postoji."),
        nameof(IdentityErrorDescriber.InvalidEmail) =>
            ("email", "Unesite ispravnu e-mail adresu u formatu ime@domena.com."),
        nameof(IdentityErrorDescriber.PasswordMismatch) =>
            ("currentPassword", "Trenutna lozinka nije ispravna."),
        _ when code.StartsWith("Password", StringComparison.Ordinal) =>
            (passwordField, "Lozinka ne ispunjava pravila sigurnosti."),
        _ => (GeneralField, "Zahtjev nije moguće obraditi. Provjerite unesene podatke."),
    };
}
