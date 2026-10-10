namespace SmartGarage.Application.Abstractions;

public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody);

/// <summary>Sends one e-mail over SMTP. Used by the Worker only; the API never sends mail.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
