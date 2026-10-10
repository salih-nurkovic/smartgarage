namespace SmartGarage.Infrastructure.Messaging;

/// <summary>Broker settings, read from <c>.env</c> once at startup.</summary>
public sealed class RabbitMqOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;

    public ushort PrefetchCount { get; set; }

    /// <summary>How many times a consumer retries a message before dead-lettering it.</summary>
    public int RetryCount { get; set; }

    /// <summary>First retry delay; each later one doubles it (1 s, 2 s, 4 s, 8 s).</summary>
    public int RetryBaseSeconds { get; set; }
}
