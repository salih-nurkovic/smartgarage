using Quartz;
using SmartGarage.Worker.Consumers;
using SmartGarage.Worker.Jobs;

namespace SmartGarage.Worker.Extensions;

/// <summary>Everything the Worker host registers on top of Infrastructure, in one call.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Each consumer is a hosted service with its own channel on the shared connection.
        services.AddHostedService<EmailConsumer>();
        services.AddHostedService<NotificationConsumer>();
        services.AddHostedService<DeadLetterConsumer>();

        // Schedules come from .env as Quartz cron expressions, so how often a job runs can be
        // changed without rebuilding.
        services.AddQuartz(quartz =>
        {
            quartz.Schedule<UnconfirmedAppointmentExpiryJob>(configuration, "CRON_PENDING_EXPIRY", "0 0/30 * * * ?");
            quartz.Schedule<AppointmentReminderJob>(configuration, "CRON_APPOINTMENT_REMINDER", "0 0 * * * ?");
            quartz.Schedule<ServiceReminderScanJob>(configuration, "CRON_SERVICE_REMINDER_SCAN", "0 0 6 * * ?");
            quartz.Schedule<PaymentReconciliationJob>(configuration, "CRON_PAYMENT_RECONCILIATION", "0 0/15 * * * ?");
        });

        // On shutdown a job that is running is allowed to finish rather than being cut off
        // half way through its batch.
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }

    private static void Schedule<TJob>(
        this IServiceCollectionQuartzConfigurator quartz,
        IConfiguration configuration,
        string cronSetting,
        string defaultCron)
        where TJob : IJob
    {
        var key = new JobKey(typeof(TJob).Name);

        quartz.AddJob<TJob>(job => job.WithIdentity(key));

        quartz.AddTrigger(trigger => trigger
            .ForJob(key)
            .WithIdentity($"{key.Name}-trigger")
            .WithCronSchedule(configuration[cronSetting] ?? defaultCron, cron => cron.InTimeZone(TimeZoneInfo.Utc)));
    }
}
