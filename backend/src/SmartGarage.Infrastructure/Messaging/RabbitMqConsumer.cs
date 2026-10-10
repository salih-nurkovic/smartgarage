using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SmartGarage.Infrastructure.Messaging;

/// <summary>A message as a consumer receives it.</summary>
public sealed record ConsumedMessage(
    string RoutingKey,
    ReadOnlyMemory<byte> Body,
    string? MessageId,
    string? CorrelationId,
    string? Type);

/// <summary>
/// Thrown by a handler for a message that can never succeed, such as one that cannot be
/// parsed. It is dead-lettered at once, because retrying it would only delay the queue.
/// </summary>
public sealed class PoisonMessageException : Exception
{
    public PoisonMessageException(string message)
        : base(message)
    {
    }

    public PoisonMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Base for every queue consumer. It owns everything the instructions (Appendix A.1) ask of
/// one, so a concrete consumer only says which queue it reads and what to do with a message:
/// <list type="bullet">
/// <item>an <see cref="AsyncEventingBasicConsumer"/>, so handlers are awaited, never blocked on;</item>
/// <item>retries with exponential backoff (1 s, 2 s, 4 s, 8 s) before giving up;</item>
/// <item>dead-lettering instead of losing a message that keeps failing;</item>
/// <item>a log entry for every failure - no exception is ever swallowed silently, and a
/// broker that is not reachable yet is retried and reported rather than ending the process.</item>
/// </list>
/// </summary>
public abstract class RabbitMqConsumer : BackgroundService
{
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger _logger;
    private IChannel? _channel;

    protected RabbitMqConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected abstract string QueueName { get; }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
    }

    /// <summary>
    /// Handles one message. The provider belongs to a scope created for this message alone,
    /// so a scoped DbContext is never shared between two messages.
    /// </summary>
    protected abstract Task HandleAsync(IServiceProvider services, ConsumedMessage message, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        // The broker may still be starting when this process does, most of all under Docker
        // Compose. Waiting and trying again is the correct response; exiting is not.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                _logger.LogInformation("Consuming queue {Queue}.", QueueName);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Any failure to connect is retried; the reason is logged each time.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                _logger.LogError(
                    exception,
                    "Could not start consuming {Queue}. Retrying in {Delay} s.",
                    QueueName, delay.TotalSeconds);

                await Task.Delay(delay, stoppingToken);
                delay = delay * 2 > MaxReconnectDelay ? MaxReconnectDelay : delay * 2;
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var channel = await _connection.CreateChannelAsync(ct);
        _channel = channel;

        await _connection.DeclareTopologyAsync(channel, ct);

        // Bounds how many unacknowledged messages the broker hands over at once.
        await channel.BasicQosAsync(prefetchSize: 0, _options.PrefetchCount, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery, ct);

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, ct);
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        // Copied, because the client reuses the delivery buffer once this handler yields.
        var message = new ConsumedMessage(
            delivery.RoutingKey,
            delivery.Body.ToArray(),
            delivery.BasicProperties.MessageId,
            delivery.BasicProperties.CorrelationId,
            delivery.BasicProperties.Type);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await HandleAsync(scope.ServiceProvider, message, ct);

                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutting down. The message was not acknowledged, so the broker delivers it
                // again when the Worker comes back.
                return;
            }
#pragma warning disable CA1031 // Every failure is handled here: retried, then dead-lettered, always logged.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                var isPoison = exception is PoisonMessageException;

                if (isPoison || attempt >= _options.RetryCount)
                {
                    _logger.LogError(
                        exception,
                        "Message {MessageId} ({RoutingKey}) on {Queue} failed after {Attempts} attempt(s) "
                        + "and is being dead-lettered.",
                        message.MessageId, message.RoutingKey, QueueName, attempt + 1);

                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, ct);
                    return;
                }

                var delay = TimeSpan.FromSeconds(_options.RetryBaseSeconds * Math.Pow(2, attempt));

                _logger.LogWarning(
                    exception,
                    "Message {MessageId} ({RoutingKey}) on {Queue} failed on attempt {Attempt}. "
                    + "Retrying in {Delay} s.",
                    message.MessageId, message.RoutingKey, QueueName, attempt + 1, delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }
    }
}
