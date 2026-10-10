using SmartGarage.Worker.Consumers;

namespace SmartGarage.Worker.Extensions;

/// <summary>Everything the Worker host registers on top of Infrastructure, in one call.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        // Each consumer is a hosted service with its own channel on the shared connection.
        services.AddHostedService<EmailConsumer>();
        services.AddHostedService<DeadLetterConsumer>();

        return services;
    }
}
