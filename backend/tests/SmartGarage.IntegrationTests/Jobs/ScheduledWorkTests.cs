using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Services;
using SmartGarage.Contracts.Events;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;
using SmartGarage.Infrastructure.Identity;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Jobs;

/// <summary>
/// What the Worker's scheduled jobs do. The jobs themselves are only timers, so the work is
/// tested where it lives - in the service layer, called as the system, the way the Worker
/// calls it.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ScheduledWorkTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ScheduledWorkTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnconfirmedBooking_IsCancelledBySystem_OnceItsTimeHasPassed()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var vehicle = await _client.CreateVehicleAsync(customer.AccessToken);
        var serviceId = (await _client.GetServicesForAsync(vehicle.EngineTypeId, customer.AccessToken))[0].Id;

        var start = TestData.Slot(11, 9);
        var forgotten = await _client.BookAsync(customer.AccessToken, vehicle.Id, start, serviceId);
        var confirmed = await _client.BookAsync(customer.AccessToken, vehicle.Id, start.AddHours(3), serviceId);
        (await _client.PostEmptyAsync($"/api/appointments/{confirmed.Id}/confirm", admin.AccessToken))
            .EnsureSuccessStatusCode();

        // Before its time, a pending booking is left alone.
        await AsSystemAsync<AppointmentService, int>(s => s.ExpireUnconfirmedAsync(CancellationToken.None));
        (await StatusOfAsync(forgotten.Id)).Should().Be(AppointmentStatus.Pending);

        try
        {
            _factory.Clock.Offset = start.AddHours(1) - DateTime.UtcNow;

            var cancelled = await AsSystemAsync<AppointmentService, int>(
                s => s.ExpireUnconfirmedAsync(CancellationToken.None));

            cancelled.Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }

        (await StatusOfAsync(confirmed.Id)).Should().Be(AppointmentStatus.Confirmed);

        var detail = await (await _client.GetAsync($"/api/appointments/{forgotten.Id}", customer.AccessToken))
            .ReadAsync<AppointmentDetailResponse>();

        detail.Status.Should().Be(AppointmentStatus.Cancelled);
        detail.CancellationReason.Should().Contain("automatski");

        // The audit trail says the system did it, not a person.
        detail.History[^1].ActorRole.Should().Be(Roles.System);
        detail.History[^1].ChangedByName.Should().Be("Sistem");
        detail.History[^1].Reason.Should().NotBeNullOrWhiteSpace();

        _factory.Events.Published.OfType<AppointmentStatusChangedEvent>()
            .Should().Contain(e => e.AppointmentId == forgotten.Id && e.ToStatus == "Cancelled" && !e.ChangedByCustomer);
    }

    [Fact]
    public async Task ScheduledWork_CannotBeTriggeredByAnyoneButTheSystem()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var expire = () => scope.ServiceProvider.GetRequiredService<IAppointmentService>()
            .ExpireUnconfirmedAsync(CancellationToken.None);
        var remind = () => scope.ServiceProvider.GetRequiredService<IReminderService>()
            .SendServiceRemindersAsync(CancellationToken.None);

        await expire.Should().ThrowAsync<ForbiddenException>();
        await remind.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ConfirmedAppointment_IsRemindedOnce_ADayAhead()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var vehicle = await _client.CreateVehicleAsync(customer.AccessToken);
        var serviceId = (await _client.GetServicesForAsync(vehicle.EngineTypeId, customer.AccessToken))[0].Id;

        var start = TestData.Slot(12, 10);
        var confirmed = await _client.BookAsync(customer.AccessToken, vehicle.Id, start, serviceId);
        var unconfirmed = await _client.BookAsync(customer.AccessToken, vehicle.Id, start.AddHours(3), serviceId);
        (await _client.PostEmptyAsync($"/api/appointments/{confirmed.Id}/confirm", admin.AccessToken))
            .EnsureSuccessStatusCode();

        // Days away: too early for a reminder.
        await AsSystemAsync<ReminderService, int>(s => s.SendAppointmentRemindersAsync(CancellationToken.None));
        Reminders().Should().NotContain(e => e.AppointmentId == confirmed.Id);

        try
        {
            _factory.Clock.Offset = start.AddHours(-10) - DateTime.UtcNow;

            await AsSystemAsync<ReminderService, int>(s => s.SendAppointmentRemindersAsync(CancellationToken.None));

            Reminders().Should().ContainSingle(e => e.AppointmentId == confirmed.Id);
            Reminders().Should().NotContain(e => e.AppointmentId == unconfirmed.Id, "it was never confirmed");

            // The Worker turns the event into this notification; once it exists the scan skips
            // the appointment, however often it runs.
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

                db.Notifications.Add(new Notification
                {
                    UserId = customer.User.Id,
                    Type = NotificationType.AppointmentReminder,
                    Title = "Podsjetnik na termin",
                    Body = "Sutra imate zakazan servis.",
                    RelatedEntityType = nameof(Appointment),
                    RelatedEntityId = confirmed.Id,
                    CreatedAtUtc = DateTime.UtcNow,
                });

                await db.SaveChangesAsync();
            }

            await AsSystemAsync<ReminderService, int>(s => s.SendAppointmentRemindersAsync(CancellationToken.None));
            Reminders().Should().ContainSingle(e => e.AppointmentId == confirmed.Id);
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Vehicle_IsFlaggedForService_WhenTheIntervalSinceTheLastOneHasPassed()
    {
        var (customer, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(customer.AccessToken);

        var service = (await _client.GetServicesForAsync(vehicle.EngineTypeId, customer.AccessToken))
            .First(s => s.RecommendedMonthsInterval is not null);

        var start = TestData.Slot(13, 9);
        await _client.BookAndCompleteAsync(_factory, customer.AccessToken, vehicle, start, service.Id);

        // Completion scheduled the next one; it is not due yet.
        (await ReminderStatusAsync(vehicle.Id)).Should().Be(ServiceReminderStatus.Pending);
        await AsSystemAsync<ReminderService, int>(s => s.SendServiceRemindersAsync(CancellationToken.None));
        ServiceReminders().Should().NotContain(e => e.VehicleId == vehicle.Id);

        try
        {
            // The recommended interval goes by.
            _factory.Clock.Offset =
                start.AddMonths(service.RecommendedMonthsInterval!.Value).AddDays(2) - DateTime.UtcNow;

            await AsSystemAsync<ReminderService, int>(s => s.SendServiceRemindersAsync(CancellationToken.None));
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }

        ServiceReminders().Should().ContainSingle(e => e.VehicleId == vehicle.Id)
            .Which.Should().Match<ServiceReminderDueEvent>(e =>
                e.OwnerId == customer.User.Id
                && e.ServiceName == service.Name
                && e.VehicleDisplay.Contains(vehicle.RegistrationPlate));

        (await ReminderStatusAsync(vehicle.Id)).Should().Be(ServiceReminderStatus.Sent);
    }

    private IEnumerable<AppointmentReminderDueEvent> Reminders() =>
        _factory.Events.Published.OfType<AppointmentReminderDueEvent>();

    private IEnumerable<ServiceReminderDueEvent> ServiceReminders() =>
        _factory.Events.Published.OfType<ServiceReminderDueEvent>();

    // Builds the service the way the Worker's container does: with the system as the caller.
    private async Task<TResult> AsSystemAsync<TService, TResult>(Func<TService, Task<TResult>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var service = ActivatorUtilities.CreateInstance<TService>(scope.ServiceProvider, new SystemUserService());
        return await work(service);
    }

    private async Task<AppointmentStatus> StatusOfAsync(int appointmentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

        return await db.Appointments.Where(a => a.Id == appointmentId).Select(a => a.Status).SingleAsync();
    }

    private async Task<ServiceReminderStatus> ReminderStatusAsync(int vehicleId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();

        return await db.ServiceReminders.Where(r => r.VehicleId == vehicleId).Select(r => r.Status).SingleAsync();
    }
}
