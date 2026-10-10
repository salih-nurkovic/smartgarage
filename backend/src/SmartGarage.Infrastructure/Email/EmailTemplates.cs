using System.Globalization;
using System.Net;
using SmartGarage.Application.Abstractions;
using SmartGarage.Contracts.Events;

namespace SmartGarage.Infrastructure.Email;

/// <summary>
/// The e-mails the system sends, one method per event. Everything that came from a user is
/// HTML-encoded before it goes into the body.
/// </summary>
public static class EmailTemplates
{
    public static EmailMessage Welcome(UserRegisteredEvent message) => new(
        message.Email,
        message.FirstName,
        "Dobro došli u SmartGarage",
        Layout(
            $"Dobro došli, {Encode(message.FirstName)}!",
            $"""
             <p>Vaš nalog je uspješno kreiran. Korisničko ime za prijavu je
             <strong>{Encode(message.UserName)}</strong>.</p>
             <p>Dodajte svoje vozilo u aplikaciji i zakažite prvi servis u nekoliko koraka.</p>
             """));

    public static EmailMessage PasswordReset(PasswordResetRequestedEvent message) => new(
        message.Email,
        message.FirstName,
        "SmartGarage - kod za promjenu lozinke",
        Layout(
            "Promjena lozinke",
            $"""
             <p>Poštovani {Encode(message.FirstName)},</p>
             <p>zaprimili smo zahtjev za promjenu lozinke. Unesite ovaj kod u aplikaciju:</p>
             <p style="font-size:28px;font-weight:bold;letter-spacing:6px;margin:24px 0;">{Encode(message.Code)}</p>
             <p>Kod vrijedi do {message.ExpiresAtUtc.ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture)} UTC
             i može se iskoristiti samo jednom.</p>
             <p>Ako niste tražili promjenu lozinke, zanemarite ovu poruku. Vaša lozinka ostaje ista.</p>
             """));

    private static string Layout(string heading, string content) =>
        $"""
         <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#1f2937;">
           <h2 style="color:#2563eb;">{heading}</h2>
           {content}
           <hr style="border:none;border-top:1px solid #e5e7eb;margin:24px 0;">
           <p style="font-size:12px;color:#6b7280;">SmartGarage - zakazivanje i upravljanje servisom vozila.
           Ovo je automatska poruka, molimo ne odgovarajte na nju.</p>
         </div>
         """;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
