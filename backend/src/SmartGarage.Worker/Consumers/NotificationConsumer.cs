using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Contracts.Events;
using SmartGarage.Contracts.Routing;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Messaging;
using SmartGarage.Infrastructure.Persistence;

namespace SmartGarage.Worker.Consumers;

/// <summary>
/// Writes notifications. Every event a user should hear about arrives here, becomes a row in
/// their inbox, and is then announced so the API can push it to them if they are online.
/// <para>
/// Doing this in the Worker keeps it off the request path: the operation that caused the
/// event has already answered its caller by the time the notification is written. The
/// instructions require a notification for every relevant event, not just one kind, so each
/// booking, status change and reschedule has its own text here.
/// </para>
/// </summary>
public sealed class NotificationConsumer : RabbitMqConsumer
{
    private const string AppointmentEntity = nameof(Appointment);
    private const string DateTimeFormat = "dd.MM.yyyy. 'u' HH:mm";

    public NotificationConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<NotificationConsumer> logger)
        : base(connection, scopeFactory, options, logger)
    {
    }

    protected override string QueueName => MessageRouting.Queues.Notifications;

    protected override async Task HandleAsync(
        IServiceProvider services,
        ConsumedMessage message,
        CancellationToken ct)
    {
        var db = services.GetRequiredService<SmartGarageDbContext>();
        var clock = services.GetRequiredService<IDateTimeProvider>();
        var publisher = services.GetRequiredService<IEventPublisher>();

        var notifications = message.RoutingKey switch
        {
            MessageRouting.RoutingKeys.UserRegistered =>
                [Welcome(message.Read<UserRegisteredEvent>())],
            MessageRouting.RoutingKeys.AppointmentCreated =>
                await ForCreatedAsync(db, message.Read<AppointmentCreatedEvent>(), ct),
            MessageRouting.RoutingKeys.AppointmentStatusChanged =>
                await ForStatusChangeAsync(db, message.Read<AppointmentStatusChangedEvent>(), ct),
            MessageRouting.RoutingKeys.AppointmentRescheduled =>
                await ForRescheduleAsync(db, message.Read<AppointmentRescheduledEvent>(), ct),
            MessageRouting.RoutingKeys.AppointmentReminderDue =>
                [AppointmentReminder(message.Read<AppointmentReminderDueEvent>())],
            MessageRouting.RoutingKeys.ServiceReminderDue =>
                [ServiceReminder(message.Read<ServiceReminderDueEvent>())],
            MessageRouting.RoutingKeys.PaymentSucceeded =>
                [PaymentSucceeded(message.Read<PaymentSucceededEvent>())],
            MessageRouting.RoutingKeys.PaymentFailed =>
                [PaymentFailed(message.Read<PaymentFailedEvent>())],
            MessageRouting.RoutingKeys.RefundIssued =>
                [RefundIssued(message.Read<RefundIssuedEvent>())],
            _ => throw new PoisonMessageException(
                $"The notification queue has no handler for routing key '{message.RoutingKey}'."),
        };

        var now = clock.UtcNow;

        foreach (var notification in notifications)
        {
            notification.CreatedAtUtc = now;
        }

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);

        // Announced only once the rows exist, so a client that reacts to the push by fetching
        // the inbox always finds them there.
        foreach (var notification in notifications)
        {
            await publisher.PublishAsync(
                new NotificationCreatedEvent
                {
                    NotificationId = notification.Id,
                    UserId = notification.UserId,
                    Type = notification.Type.ToString(),
                    Title = notification.Title,
                    Body = notification.Body,
                    CreatedAtUtc = notification.CreatedAtUtc,
                    RelatedEntityType = notification.RelatedEntityType,
                    RelatedEntityId = notification.RelatedEntityId,
                    CorrelationId = message.CorrelationId,
                },
                MessageRouting.RoutingKeys.NotificationCreated,
                ct);
        }
    }

    private static Notification Welcome(UserRegisteredEvent registered) => new()
    {
        UserId = registered.UserId,
        Type = NotificationType.System,
        Title = "Dobro došli u SmartGarage",
        Body = $"Zdravo {registered.FirstName}, vaš nalog je spreman. Dodajte vozilo i zakažite prvi servis.",
    };

    private static Notification AppointmentReminder(AppointmentReminderDueEvent due) => About(
        due.CustomerId,
        due.AppointmentId,
        NotificationType.AppointmentReminder,
        "Podsjetnik na termin",
        $"Podsjećamo vas da {Format(due.ScheduledStartUtc)} imate zakazan servis (rezervacija {due.AppointmentNumber}).");

    private static Notification ServiceReminder(ServiceReminderDueEvent due) => new()
    {
        UserId = due.OwnerId,
        Type = NotificationType.ServiceReminder,
        Title = "Vrijeme je za servis",
        Body = $"Za vozilo {due.VehicleDisplay} je vrijeme za uslugu „{due.ServiceName}“. Zakažite termin u aplikaciji.",
        RelatedEntityType = nameof(Vehicle),
        RelatedEntityId = due.VehicleId,
    };

    private static Notification PaymentSucceeded(PaymentSucceededEvent paid) => About(
        paid.PayerId,
        paid.AppointmentId,
        NotificationType.PaymentSucceeded,
        "Plaćanje uspješno",
        $"Uspješno ste platili {Money(paid.Amount, paid.Currency)} za rezervaciju {paid.AppointmentNumber} "
        + $"(uplata {paid.PaymentNumber}).");

    private static Notification PaymentFailed(PaymentFailedEvent failed) => About(
        failed.PayerId,
        failed.AppointmentId,
        NotificationType.PaymentFailed,
        "Plaćanje nije uspjelo",
        $"Plaćanje za rezervaciju {failed.AppointmentNumber} nije prošlo. "
        + (string.IsNullOrWhiteSpace(failed.Reason) ? string.Empty : $"Razlog: {failed.Reason} ")
        + "Možete pokušati ponovo.");

    private static Notification RefundIssued(RefundIssuedEvent refunded) => About(
        refunded.PayerId,
        refunded.AppointmentId,
        NotificationType.RefundIssued,
        "Povrat novca",
        $"Vraćeno vam je {Money(refunded.Amount, refunded.Currency)} za uplatu {refunded.PaymentNumber}. "
        + $"Razlog: {refunded.Reason}");

    private static string Money(decimal amount, string currency) =>
        $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";

    private static async Task<List<Notification>> ForCreatedAsync(
        SmartGarageDbContext db,
        AppointmentCreatedEvent created,
        CancellationToken ct)
    {
        var when = Format(created.ScheduledStartUtc);

        var notifications = new List<Notification>
        {
            About(
                created.CustomerId,
                created.AppointmentId,
                NotificationType.AppointmentCreated,
                "Rezervacija zaprimljena",
                $"Zaprimili smo vašu rezervaciju {created.AppointmentNumber} za {when}. "
                + "Obavijestit ćemo vas kada bude potvrđena."),
        };

        // A new booking is something the desk has to act on, so the administrators hear of it too.
        notifications.AddRange((await AdministratorIdsAsync(db, ct)).Select(adminId => About(
            adminId,
            created.AppointmentId,
            NotificationType.AppointmentCreated,
            "Nova rezervacija čeka potvrdu",
            $"{created.CustomerFullName} je zakazao/la termin {created.AppointmentNumber} za {when}.")));

        return notifications;
    }

    private static async Task<List<Notification>> ForStatusChangeAsync(
        SmartGarageDbContext db,
        AppointmentStatusChangedEvent changed,
        CancellationToken ct)
    {
        if (!Enum.TryParse<AppointmentStatus>(changed.ToStatus, out var status))
        {
            throw new PoisonMessageException($"Unknown appointment status '{changed.ToStatus}'.");
        }

        var number = changed.AppointmentNumber;
        var when = Format(changed.ScheduledStartUtc);

        var (type, title, body) = status switch
        {
            AppointmentStatus.Confirmed => (
                NotificationType.AppointmentConfirmed,
                "Rezervacija potvrđena",
                $"Vaša rezervacija {number} je potvrđena za {when}."),
            AppointmentStatus.InProgress => (
                NotificationType.AppointmentConfirmed,
                "Servis u toku",
                $"Rad na vašem vozilu je započet (rezervacija {number})."),
            AppointmentStatus.Completed => (
                NotificationType.AppointmentCompleted,
                "Servis završen",
                $"Servis po rezervaciji {number} je završen. Možete izvršiti plaćanje i ostaviti recenziju."),
            AppointmentStatus.Cancelled => (
                NotificationType.AppointmentCancelled,
                "Rezervacija otkazana",
                $"Rezervacija {number} za {when} je otkazana. Razlog: {changed.Reason}"),
            _ => throw new PoisonMessageException($"No notification is defined for status '{status}'."),
        };

        var notifications = new List<Notification> { About(changed.CustomerId, changed.AppointmentId, type, title, body) };

        // When the customer is the one who cancelled, it is the garage that needs telling.
        if (status == AppointmentStatus.Cancelled && changed.ChangedByCustomer)
        {
            notifications.AddRange((await AdministratorIdsAsync(db, ct)).Select(adminId => About(
                adminId,
                changed.AppointmentId,
                NotificationType.AppointmentCancelled,
                "Klijent je otkazao rezervaciju",
                $"{changed.CustomerFullName} je otkazao/la rezervaciju {number} za {when}. Razlog: {changed.Reason}")));
        }

        return notifications;
    }

    private static async Task<List<Notification>> ForRescheduleAsync(
        SmartGarageDbContext db,
        AppointmentRescheduledEvent moved,
        CancellationToken ct)
    {
        var change = $"sa {Format(moved.OldStartUtc)} na {Format(moved.NewStartUtc)}";
        var reason = string.IsNullOrWhiteSpace(moved.Reason) ? string.Empty : $" Razlog: {moved.Reason}";

        var notifications = new List<Notification>
        {
            About(
                moved.CustomerId,
                moved.AppointmentId,
                NotificationType.AppointmentRescheduled,
                "Promjena termina",
                $"Termin rezervacije {moved.AppointmentNumber} je pomjeren {change}.{reason}"),
        };

        if (moved.ChangedByCustomer)
        {
            notifications.AddRange((await AdministratorIdsAsync(db, ct)).Select(adminId => About(
                adminId,
                moved.AppointmentId,
                NotificationType.AppointmentRescheduled,
                "Klijent je promijenio termin",
                $"{moved.CustomerFullName} je pomjerio/la rezervaciju {moved.AppointmentNumber} {change}.{reason}")));
        }

        return notifications;
    }

    private static Notification About(int userId, int appointmentId, NotificationType type, string title, string body) =>
        new()
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            RelatedEntityType = AppointmentEntity,
            RelatedEntityId = appointmentId,
        };

    private static Task<List<int>> AdministratorIdsAsync(SmartGarageDbContext db, CancellationToken ct) =>
        db.UserRoles
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .Where(x => x.Name == Roles.Administrator)
            .Join(db.Users.Where(u => u.IsActive), x => x.UserId, u => u.Id, (x, u) => u.Id)
            .ToListAsync(ct);

    private static string Format(DateTime value) => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
}
