using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Contracts.Routing;
using SmartGarage.Domain.Entities;
using SmartGarage.Infrastructure.Messaging;
using SmartGarage.Infrastructure.Persistence;

namespace SmartGarage.Worker.Consumers;

/// <summary>
/// Records every message that failed all its retries in the <c>ErrorLogs</c> table, where an
/// administrator can see it from the desktop application.
/// <para>
/// Without this a message that kept failing would sit in a queue nobody looks at. The body is
/// deliberately not stored: it can contain a password reset code.
/// </para>
/// </summary>
public sealed class DeadLetterConsumer : RabbitMqConsumer
{
    private const string WorkerSource = "Worker";
    private const string ErrorLevel = "Error";

    public DeadLetterConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<DeadLetterConsumer> logger)
        : base(connection, scopeFactory, options, logger)
    {
    }

    protected override string QueueName => MessageRouting.Queues.DeadLetter;

    protected override async Task HandleAsync(
        IServiceProvider services,
        ConsumedMessage message,
        CancellationToken ct)
    {
        var db = services.GetRequiredService<SmartGarageDbContext>();
        var clock = services.GetRequiredService<IDateTimeProvider>();

        db.ErrorLogs.Add(new ErrorLog
        {
            CorrelationId = message.CorrelationId ?? message.MessageId ?? string.Empty,
            Level = ErrorLevel,
            Message =
                $"Message {message.MessageId} of type {message.Type} could not be processed "
                + "after all retries and was dead-lettered.",
            RequestPath = message.RoutingKey,
            Source = WorkerSource,
            OccurredAtUtc = clock.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
