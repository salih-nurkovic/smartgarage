using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartGarage.Application.Abstractions;

namespace SmartGarage.Infrastructure.Email;

/// <summary>
/// Stands in for e-mail delivery until the Worker and RabbitMQ exist (phase 5), at which point
/// a publisher that hands the message to the Worker replaces this class.
/// <para>
/// In Development the code is written to the log so the reset flow can be exercised end to
/// end. Outside Development it is never logged: a reset code in a log file is a password
/// anyone with log access can use.
/// </para>
/// </summary>
public sealed class DevelopmentPasswordResetCodeSender : IPasswordResetCodeSender
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DevelopmentPasswordResetCodeSender> _logger;

    public DevelopmentPasswordResetCodeSender(
        IHostEnvironment environment,
        ILogger<DevelopmentPasswordResetCodeSender> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendAsync(PasswordResetCodeMessage message, CancellationToken cancellationToken)
    {
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "Password reset code for {Email}: {Code} (valid until {ExpiresAtUtc:u}).",
                message.Email, message.Code, message.ExpiresAtUtc);
        }
        else
        {
            _logger.LogWarning(
                "A password reset code was generated for {Email} but e-mail delivery is not "
                + "configured, so it was not sent.",
                message.Email);
        }

        return Task.CompletedTask;
    }
}
