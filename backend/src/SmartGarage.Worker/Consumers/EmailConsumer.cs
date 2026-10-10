using System.Text.Json;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Contracts.Events;
using SmartGarage.Contracts.Routing;
using SmartGarage.Infrastructure.Email;
using SmartGarage.Infrastructure.Messaging;

namespace SmartGarage.Worker.Consumers;

/// <summary>
/// Turns events into e-mails. This is the work the API hands off so that an SMTP round trip
/// never sits inside an HTTP request.
/// </summary>
public sealed class EmailConsumer : RabbitMqConsumer
{
    private readonly ILogger<EmailConsumer> _logger;

    public EmailConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<EmailConsumer> logger)
        : base(connection, scopeFactory, options, logger)
    {
        _logger = logger;
    }

    protected override string QueueName => MessageRouting.Queues.Email;

    protected override async Task HandleAsync(
        IServiceProvider services,
        ConsumedMessage message,
        CancellationToken ct)
    {
        var email = message.RoutingKey switch
        {
            MessageRouting.RoutingKeys.UserRegistered =>
                EmailTemplates.Welcome(Read<UserRegisteredEvent>(message)),
            MessageRouting.RoutingKeys.PasswordResetRequested =>
                EmailTemplates.PasswordReset(Read<PasswordResetRequestedEvent>(message)),
            _ => throw new PoisonMessageException(
                $"The e-mail queue has no handler for routing key '{message.RoutingKey}'."),
        };

        await services.GetRequiredService<IEmailSender>().SendAsync(email, ct);

        _logger.LogInformation(
            "Sent '{Subject}' to {Recipient} for message {MessageId}.",
            email.Subject, email.ToAddress, message.MessageId);
    }

    private static TEvent Read<TEvent>(ConsumedMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<TEvent>(message.Body.Span)
                   ?? throw new PoisonMessageException("The message body is empty.");
        }
        catch (JsonException exception)
        {
            // Malformed JSON will be malformed on every retry too.
            throw new PoisonMessageException(
                $"The body of message {message.MessageId} is not valid {typeof(TEvent).Name} JSON.",
                exception);
        }
    }
}
