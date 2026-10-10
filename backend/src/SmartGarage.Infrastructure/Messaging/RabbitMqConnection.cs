using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SmartGarage.Contracts.Routing;

namespace SmartGarage.Infrastructure.Messaging;

/// <summary>
/// The process's one connection to RabbitMQ.
/// <para>
/// The instructions (Appendix A.1) forbid opening a connection per publish. This class is
/// registered as a singleton and opens the connection on first use; every publisher and
/// consumer after that takes a channel from the same connection.
/// </para>
/// </summary>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken ct)
    {
        var connection = await GetConnectionAsync(ct);
        return await connection.CreateChannelAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Declares the exchanges, queues and bindings. Declaring is idempotent, so the API and
    /// the Worker both do it and neither depends on the other having started first.
    /// </summary>
    public async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            _options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            MessageRouting.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            MessageRouting.Queues.DeadLetter, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            MessageRouting.Queues.DeadLetter, MessageRouting.DeadLetterExchange, routingKey: string.Empty,
            cancellationToken: ct);

        // A message this queue rejects without requeue goes to the dead-letter exchange
        // instead of being dropped.
        var deadLettering = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = MessageRouting.DeadLetterExchange,
        };

        await channel.QueueDeclareAsync(
            MessageRouting.Queues.Email, durable: true, exclusive: false, autoDelete: false,
            arguments: deadLettering, cancellationToken: ct);

        foreach (var routingKey in MessageRouting.EmailBindings)
        {
            await channel.QueueBindAsync(
                MessageRouting.Queues.Email, _options.Exchange, routingKey, cancellationToken: ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(ct);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,

                // A broker restart is survived without restarting the application: the client
                // reconnects and re-declares what it had.
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = AppDomain.CurrentDomain.FriendlyName,
            };

            _connection = await factory.CreateConnectionAsync(ct);

            _logger.LogInformation(
                "Connected to RabbitMQ at {Host}:{Port}.", _options.Host, _options.Port);

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }
}
