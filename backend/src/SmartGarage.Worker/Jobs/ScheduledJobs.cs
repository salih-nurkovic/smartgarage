using Quartz;
using SmartGarage.Application.Services;

namespace SmartGarage.Worker.Jobs;

// The Worker's scheduled work. Each job is only the timer: what it does lives in the service
// layer, where the same rules apply as for a person doing it by hand. A failure is logged and
// rethrown so Quartz records the run as failed; the next run picks the work up again.

/// <summary>Cancels bookings nobody confirmed before their time came.</summary>
[DisallowConcurrentExecution]
public sealed class UnconfirmedAppointmentExpiryJob(
    IAppointmentService appointments,
    ILogger<UnconfirmedAppointmentExpiryJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancelled = await appointments.ExpireUnconfirmedAsync(context.CancellationToken);

        if (cancelled > 0)
        {
            logger.LogInformation("Cancelled {Count} unconfirmed appointment(s).", cancelled);
        }
    }
}

/// <summary>Reminds customers of confirmed appointments that are a day away.</summary>
[DisallowConcurrentExecution]
public sealed class AppointmentReminderJob(
    IReminderService reminders,
    ILogger<AppointmentReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var sent = await reminders.SendAppointmentRemindersAsync(context.CancellationToken);

        if (sent > 0)
        {
            logger.LogInformation("Queued {Count} appointment reminder(s).", sent);
        }
    }
}

/// <summary>Settles payments whose webhook never arrived, by asking Stripe directly.</summary>
[DisallowConcurrentExecution]
public sealed class PaymentReconciliationJob(
    IPaymentService payments,
    ILogger<PaymentReconciliationJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var checkedCount = await payments.ReconcileStaleAsync(context.CancellationToken);

        if (checkedCount > 0)
        {
            logger.LogInformation("Reconciled {Count} open payment(s) with Stripe.", checkedCount);
        }
    }
}

/// <summary>Tells owners when a vehicle becomes due for a service, by date or by mileage.</summary>
[DisallowConcurrentExecution]
public sealed class ServiceReminderScanJob(
    IReminderService reminders,
    ILogger<ServiceReminderScanJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var sent = await reminders.SendServiceRemindersAsync(context.CancellationToken);

        if (sent > 0)
        {
            logger.LogInformation("Queued {Count} service reminder(s).", sent);
        }
    }
}
