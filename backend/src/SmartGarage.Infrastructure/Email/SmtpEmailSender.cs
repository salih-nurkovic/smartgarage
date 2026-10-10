using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SmartGarage.Application.Abstractions;

namespace SmartGarage.Infrastructure.Email;

/// <summary>SMTP settings, read from <c>.env</c> once at startup.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; }

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
}

/// <summary>
/// Sends real SMTP mail through MailKit. Nothing here is simulated: in development the
/// server on the other end is MailHog, and pointing it at a real provider is a change to
/// <c>.env</c> only.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var mail = new MimeMessage();

        mail.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mail.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mail.Subject = message.Subject;
        mail.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            ct);

        if (!string.IsNullOrEmpty(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, ct);
        }

        await client.SendAsync(mail, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
