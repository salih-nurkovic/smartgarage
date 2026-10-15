using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Notifications;
using SmartGarage.Contracts.Events;
using SmartGarage.Contracts.Routing;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Messaging;

namespace SmartGarage.API.Consumers;

/// <summary>
/// Takes notifications the Worker has stored and pushes them to connected clients.
/// <para>
/// This lives in the API because the SignalR connections live here; the Worker has no way to
/// reach a client's socket. It is not the helper microservice the instructions ask for - that
/// is the Worker, a separate project and process. This is only the last hop of its output.
/// </para>
/// </summary>
public sealed class RealtimeFanoutConsumer : RabbitMqConsumer
{
    public RealtimeFanoutConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<RealtimeFanoutConsumer> logger)
        : base(connection, scopeFactory, options, logger)
    {
    }

    protected override string QueueName => MessageRouting.Queues.Realtime;

    protected override Task HandleAsync(IServiceProvider services, ConsumedMessage message, CancellationToken ct)
    {
        var created = message.Read<NotificationCreatedEvent>();

        return Enum.TryParse<NotificationType>(created.Type, out var type)
            ? services.GetRequiredService<IRealtimeNotifier>().NotificationReceivedAsync(
                created.UserId,
                new NotificationResponse
                {
                    Id = created.NotificationId,
                    Type = type,
                    Title = created.Title,
                    Body = created.Body,
                    IsRead = false,
                    CreatedAtUtc = created.CreatedAtUtc,
                    RelatedEntityType = created.RelatedEntityType,
                    RelatedEntityId = created.RelatedEntityId,
                },
                ct)
            : throw new PoisonMessageException(
                $"Message {message.MessageId} carries the unknown notification type '{created.Type}'.");
    }
}
