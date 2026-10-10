using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SmartGarage.Application.Abstractions;
using SmartGarage.Contracts.Events;

namespace SmartGarage.Infrastructure.Messaging;

/// <summary>
/// Publishes events to the topic exchange as persistent JSON messages.
/// <para>
/// One channel is kept open and used by one publish at a time. A channel is not safe for
/// concurrent publishing, and opening one per message would waste a round trip on every
/// request.
/// </para>
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    // A request should not hang for the client library's full connection timeout just because
    // the broker is down.
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public RabbitMqEventPublisher(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent message, string routingKey, CancellationToken ct)
        where TEvent : IntegrationEvent
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PublishTimeout);

        try
        {
            await _gate.WaitAsync(timeout.Token);

            try
            {
                var channel = await GetChannelAsync(timeout.Token);

                var properties = new BasicProperties
                {
                    // Survives a broker restart, together with the durable queue it lands in.
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = message.EventId.ToString(),
                    CorrelationId = message.CorrelationId,
                    Type = typeof(TEvent).Name,
                };

                await channel.BasicPublishAsync(
                    _options.Exchange,
                    routingKey,
                    mandatory: false,
                    properties,
                    JsonSerializer.SerializeToUtf8Bytes(message),
                    timeout.Token);
            }
            finally
            {
                _gate.Release();
            }

            _logger.LogInformation(
                "Published {EventType} {EventId} with routing key {RoutingKey}.",
                typeof(TEvent).Name, message.EventId, routingKey);
        }
#pragma warning disable CA1031 // By contract a failed publish must not fail the operation that caused it.
        catch (Exception exception) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            // Not swallowed: logged as an error with the event id, so the lost message can be
            // identified and the cause (usually the broker being down) is on record.
            _logger.LogError(
                exception,
                "Could not publish {EventType} {EventId} with routing key {RoutingKey}.",
                typeof(TEvent).Name, message.EventId, routingKey);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _gate.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _channel = await _connection.CreateChannelAsync(ct);
        await _connection.DeclareTopologyAsync(_channel, ct);

        return _channel;
    }
}
