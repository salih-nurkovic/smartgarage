using System.Net;
using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Application.Dtos.Employees;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Contracts.Events;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Enums;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Appointments;

[Collection(ApiTestGroup.Name)]
public sealed class AppointmentTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AppointmentTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Booking_ComputesPriceAndEndOnTheServer_AndStartsAsPending()
    {
        var customer = await NewCustomerAsync();
        var services = customer.Services.Take(2).ToList();
        var start = TestData.Slot(dayIndex: 0, hour: 10);

        // A price sent by the client has nowhere to go: the request has no such field.
        var response = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/appointments",
            new
            {
                vehicleId = customer.Vehicle.Id,
                serviceIds = services.Select(s => s.Id),
                scheduledStartUtc = start,
                customerNotes = "  Čuje se buka pri kočenju.  ",
                totalPrice = 0.01,
                status = "Completed",
                customerId = 1,
            },
            customer.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var appointment = await response.ReadAsync<AppointmentDetailResponse>();

        appointment.AppointmentNumber.Should().MatchRegex(@"^APP-\d{4}-\d{6}$");
        appointment.Status.Should().Be(AppointmentStatus.Pending);
        appointment.PaymentStatus.Should().Be(AppointmentPaymentStatus.Unpaid);
        appointment.TotalPrice.Should().Be(services.Sum(s => s.Price));
        appointment.ScheduledStartUtc.Should().Be(start);
        appointment.ScheduledEndUtc.Should().Be(start.AddMinutes(services.Sum(s => s.EstimatedDurationMinutes)));
        appointment.CustomerNotes.Should().Be("Čuje se buka pri kočenju.");
        appointment.CustomerFullName.Should().Be($"{customer.Auth.User.FirstName} {customer.Auth.User.LastName}");
        appointment.VehicleDisplay.Should().Contain(customer.Vehicle.RegistrationPlate);

        appointment.Items.Should().HaveCount(2);
        appointment.Items.Should().OnlyContain(i => i.LineTotal == i.UnitPrice && i.Quantity == 1);
        appointment.History.Should().ContainSingle()
            .Which.Should().Match<AppointmentHistoryResponse>(h =>
                h.FromStatus == null && h.ToStatus == AppointmentStatus.Pending && h.ActorRole == Roles.Customer);

        appointment.AllowedActions.Should().BeEquivalentTo(AppointmentActions.Cancel, AppointmentActions.Reschedule);
        appointment.CanBePaid.Should().BeFalse();
        appointment.PaymentBlockedReason.Should().NotBeNullOrWhiteSpace();

        _factory.Events.Published.OfType<AppointmentCreatedEvent>()
            .Should().ContainSingle(e => e.AppointmentId == appointment.Id);
    }

    [Fact]
    public async Task Booking_IsRefused_WhenTheRequestIsInvalidOrCollidesWithAnotherBooking()
    {
        var customer = await NewCustomerAsync();
        var stranger = await NewCustomerAsync();
        var serviceId = customer.Services[0].Id;
        var start = TestData.Slot(dayIndex: 1, hour: 9);

        (await TryBookAsync(customer.Token, customer.Vehicle.Id, start.AddMinutes(30), serviceId))
            .Should().Be((HttpStatusCode.BadRequest, "scheduledStartUtc"));

        (await TryBookAsync(customer.Token, customer.Vehicle.Id, start.AddHours(12), serviceId))
            .Should().Be((HttpStatusCode.BadRequest, "scheduledStartUtc"));

        (await TryBookAsync(customer.Token, stranger.Vehicle.Id, start, serviceId))
            .Should().Be((HttpStatusCode.BadRequest, "vehicleId"));

        (await TryBookAsync(customer.Token, customer.Vehicle.Id, start, 999_999))
            .Should().Be((HttpStatusCode.BadRequest, "serviceIds"));

        (await TryBookAsync(customer.Token, customer.Vehicle.Id, start))
            .Should().Be((HttpStatusCode.BadRequest, "serviceIds"));

        var yesterday = DateTime.UtcNow.Date.AddDays(-1).AddHours(10);
        (await TryBookAsync(customer.Token, customer.Vehicle.Id, yesterday, serviceId)).Status
            .Should().Be(HttpStatusCode.Conflict);

        // A service that lists engine types, none of which is this vehicle's.
        var all = (await (await _client.GetAsync("/api/services?pageSize=100", customer.Token))
            .ReadAsync<PagedResult<ServiceResponse>>()).Items;
        var unsuitable = all.First(
            s => s.EngineTypes.Count > 0 && s.EngineTypes.All(e => e.Id != customer.Vehicle.EngineTypeId));

        (await TryBookAsync(customer.Token, customer.Vehicle.Id, start, unsuitable.Id))
            .Should().Be((HttpStatusCode.BadRequest, "serviceIds"));

        await _client.BookAsync(customer.Token, customer.Vehicle.Id, start, serviceId);

        // The same vehicle cannot be in two places, and the same customer cannot hold the same hour twice.
        (await TryBookAsync(customer.Token, customer.Vehicle.Id, start, serviceId)).Status
            .Should().Be(HttpStatusCode.Conflict);

        var secondVehicle = await _client.CreateVehicleAsync(customer.Token);
        (await TryBookAsync(customer.Token, secondVehicle.Id, start, serviceId)).Status
            .Should().Be(HttpStatusCode.Conflict);

        (await TryBookAsync(customer.Token, secondVehicle.Id, start.AddHours(2), serviceId)).Status
            .Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Garage_TakesFiveVehiclesAtOnce_AndTheLastPlaceGoesToExactlyOneOfTwoRacingCustomers()
    {
        var start = TestData.Slot(dayIndex: 2, hour: 11);
        var day = DateOnly.FromDateTime(start);

        for (var i = 0; i < 4; i++)
        {
            var customer = await NewCustomerAsync();
            await _client.BookAsync(customer.Token, customer.Vehicle.Id, start, customer.Services[0].Id);
        }

        var first = await NewCustomerAsync();
        var second = await NewCustomerAsync();

        // Both ask for the fifth and last place at the same moment.
        var results = await Task.WhenAll(
            TryBookAsync(first.Token, first.Vehicle.Id, start, first.Services[0].Id),
            TryBookAsync(second.Token, second.Vehicle.Id, start, second.Services[0].Id));

        results.Select(r => r.Status).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);

        var slots = await (await _client.GetAsync(
                $"/api/availability/slots?date={day:yyyy-MM-dd}&serviceIds={first.Services[0].Id}", first.Token))
            .ReadAsync<List<SlotResponse>>();

        slots.Select(s => s.StartUtc.Hour).Should().Equal(8, 9, 10, 11, 12, 13, 14, 15, 16);

        var full = slots.Single(s => s.StartUtc == start);
        full.IsAvailable.Should().BeFalse();
        full.FreePlaces.Should().Be(0);
        full.UnavailableReason.Should().NotBeNullOrWhiteSpace();

        slots.Single(s => s.StartUtc == start.AddHours(3)).IsAvailable.Should().BeTrue();

        (await _client.GetAsync(
                $"/api/availability/slots?date={day:yyyy-MM-dd}&serviceIds=999999", first.Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Appointment_MovesThroughTheWholeWorkflow_LeavingAnAuditTrail()
    {
        var customer = await NewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var employee = await _client.LoginAsync("employee");
        var start = TestData.Slot(dayIndex: 3, hour: 10);

        var mechanic = (await (await _client.GetAsync("/api/employees?search=Adnan", admin.AccessToken))
            .ReadAsync<PagedResult<EmployeeResponse>>()).Items.Single();

        var booked = await _client.BookAsync(customer.Token, customer.Vehicle.Id, start, customer.Services[0].Id);
        var url = $"/api/appointments/{booked.Id}";

        // A customer cannot approve their own booking.
        (await _client.PostEmptyAsync($"{url}/confirm", customer.Token))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var confirmed = await (await _client.PostEmptyAsync($"{url}/confirm", employee.AccessToken))
            .ReadAsync<AppointmentDetailResponse>();
        confirmed.Status.Should().Be(AppointmentStatus.Confirmed);
        confirmed.ConfirmedAtUtc.Should().NotBeNull();

        // Confirming twice is not a transition.
        (await _client.PostEmptyAsync($"{url}/confirm", employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Nobody is assigned yet, so work cannot start.
        (await _client.PostEmptyAsync($"{url}/start", employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        (await _client.SendJsonAsync(
                HttpMethod.Post, $"{url}/assign", new AppointmentAssignRequest { EmployeeId = mechanic.Id }, employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var assigned = await (await _client.SendJsonAsync(
                HttpMethod.Post, $"{url}/assign", new AppointmentAssignRequest { EmployeeId = mechanic.Id }, admin.AccessToken))
            .ReadAsync<AppointmentDetailResponse>();
        assigned.AssignedEmployeeName.Should().Be($"{mechanic.FirstName} {mechanic.LastName}");
        assigned.AssignedEmployeePhone.Should().Be(mechanic.PhoneNumber);

        // Still days away: the vehicle is not in the garage yet.
        (await _client.PostEmptyAsync($"{url}/start", employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        try
        {
            // The day of the appointment arrives.
            _factory.Clock.Offset = start.AddMinutes(5) - DateTime.UtcNow;

            var started = await (await _client.PostEmptyAsync($"{url}/start", employee.AccessToken))
                .ReadAsync<AppointmentDetailResponse>();
            started.Status.Should().Be(AppointmentStatus.InProgress);

            // Work is under way: stopping it now is an administrator's decision alone.
            started.AllowedActions.Should().Contain(AppointmentActions.Complete);
            (await CancelAsync(url, customer.Token, "Predomislio sam se.")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await CancelAsync(url, employee.AccessToken, "Nema dijelova.")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);

            var tooLow = await _client.SendJsonAsync(
                HttpMethod.Post,
                $"{url}/complete",
                new AppointmentCompleteRequest { MileageAtAppointmentKm = customer.Vehicle.CurrentMileageKm - 1 },
                employee.AccessToken);
            tooLow.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var completed = await (await _client.SendJsonAsync(
                    HttpMethod.Post,
                    $"{url}/complete",
                    new AppointmentCompleteRequest
                    {
                        MileageAtAppointmentKm = customer.Vehicle.CurrentMileageKm + 1200,
                        InternalNotes = "Zamijenjen i zaptivač.",
                    },
                    employee.AccessToken))
                .ReadAsync<AppointmentDetailResponse>();

            completed.Status.Should().Be(AppointmentStatus.Completed);
            completed.CompletedAtUtc.Should().NotBeNull();
            completed.InternalNotes.Should().Be("Zamijenjen i zaptivač.");
            completed.AllowedActions.Should().BeEmpty();

            // Terminal: nothing moves a completed appointment.
            (await CancelAsync(url, admin.AccessToken, "Pokušaj nakon završetka.")).StatusCode
                .Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }

        var seenByCustomer = await (await _client.GetAsync(url, customer.Token)).ReadAsync<AppointmentDetailResponse>();

        seenByCustomer.InternalNotes.Should().BeNull();
        seenByCustomer.CanBePaid.Should().BeTrue();
        seenByCustomer.AllowedActions.Should().Equal(AppointmentActions.LeaveReview);

        seenByCustomer.History.Select(h => h.ToStatus).Should().Equal(
            AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.InProgress, AppointmentStatus.Completed);
        seenByCustomer.History.Select(h => h.ActorRole).Should().Equal(
            Roles.Customer, Roles.Employee, Roles.Employee, Roles.Employee);
        seenByCustomer.History.Should().OnlyContain(h => !string.IsNullOrWhiteSpace(h.ChangedByName));
        seenByCustomer.History.Select(h => h.ChangedAtUtc).Should().BeInAscendingOrder();

        // Completion updated the odometer and counted the job for the mechanic.
        var vehicle = await (await _client.GetAsync($"/api/vehicles/{customer.Vehicle.Id}", customer.Token))
            .ReadAsync<VehicleResponse>();
        vehicle.CurrentMileageKm.Should().Be(customer.Vehicle.CurrentMileageKm + 1200);
        vehicle.LastServiceAtUtc.Should().NotBeNull();

        (await (await _client.GetAsync($"/api/employees/{mechanic.Id}", admin.AccessToken))
            .ReadAsync<EmployeeResponse>()).CompletedJobs.Should().Be(mechanic.CompletedJobs + 1);

        _factory.Events.Published.OfType<AppointmentStatusChangedEvent>()
            .Where(e => e.AppointmentId == booked.Id)
            .Select(e => e.ToStatus)
            .Should().Equal("Confirmed", "InProgress", "Completed");
    }

    [Fact]
    public async Task Cancellation_NeedsAReason_AndACustomerCannotCancelCloseToTheAppointment()
    {
        var customer = await NewCustomerAsync();
        var stranger = await NewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var serviceId = customer.Services[0].Id;

        var first = await _client.BookAsync(customer.Token, customer.Vehicle.Id, TestData.Slot(4, 10), serviceId);
        var firstUrl = $"/api/appointments/{first.Id}";

        // Somebody else's appointment does not exist as far as this customer can tell.
        (await _client.GetAsync(firstUrl, stranger.Token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CancelAsync(firstUrl, stranger.Token, "Tuđa rezervacija.")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var withoutReason = await CancelAsync(firstUrl, customer.Token, " ");
        withoutReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await withoutReason.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("reason");

        var cancelled = await (await CancelAsync(firstUrl, customer.Token, "Vozilo je prodano."))
            .ReadAsync<AppointmentDetailResponse>();

        cancelled.Status.Should().Be(AppointmentStatus.Cancelled);
        cancelled.CancellationReason.Should().Be("Vozilo je prodano.");
        cancelled.CancelledAtUtc.Should().NotBeNull();
        cancelled.AllowedActions.Should().BeEmpty();
        cancelled.History[^1].Reason.Should().Be("Vozilo je prodano.");

        _factory.Events.Published.OfType<AppointmentStatusChangedEvent>()
            .Single(e => e.AppointmentId == first.Id)
            .Should().Match<AppointmentStatusChangedEvent>(e => e.ChangedByCustomer && e.Reason == "Vozilo je prodano.");

        // The place it held is free again.
        var start = TestData.Slot(4, 12);
        var second = await _client.BookAsync(customer.Token, customer.Vehicle.Id, start, serviceId);
        var secondUrl = $"/api/appointments/{second.Id}";

        try
        {
            // Five hours before the appointment: inside the 24-hour cut-off.
            _factory.Clock.Offset = start.AddHours(-5) - DateTime.UtcNow;

            var late = await CancelAsync(secondUrl, customer.Token, "Ne mogu doći.");
            late.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            var detail = await (await _client.GetAsync(secondUrl, customer.Token)).ReadAsync<AppointmentDetailResponse>();
            detail.AllowedActions.Should().BeEmpty();
            detail.ChangeBlockedReason.Should().Contain("24");

            (await _client.SendJsonAsync(
                    HttpMethod.Put,
                    $"{secondUrl}/reschedule",
                    new AppointmentRescheduleRequest { NewStartUtc = TestData.Slot(5, 9) },
                    customer.Token))
                .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            // The garage itself is not bound by the cut-off.
            (await CancelAsync(secondUrl, admin.AccessToken, "Mehaničar je na bolovanju."))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _factory.Clock.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Reschedule_MovesTheAppointment_ThroughTheSameChecksAsABooking()
    {
        var customer = await NewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var serviceId = customer.Services[0].Id;
        var oldStart = TestData.Slot(6, 9);
        var newStart = TestData.Slot(7, 14);

        var booked = await _client.BookAsync(customer.Token, customer.Vehicle.Id, oldStart, serviceId);
        var url = $"/api/appointments/{booked.Id}/reschedule";

        (await _client.SendJsonAsync(
                HttpMethod.Put, url, new AppointmentRescheduleRequest { NewStartUtc = newStart.AddMinutes(20) }, customer.Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.SendJsonAsync(
                HttpMethod.Put, url, new AppointmentRescheduleRequest { NewStartUtc = oldStart }, customer.Token))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var moved = await (await _client.SendJsonAsync(
                HttpMethod.Put,
                url,
                new AppointmentRescheduleRequest { NewStartUtc = newStart, Reason = "Službeni put." },
                customer.Token))
            .ReadAsync<AppointmentDetailResponse>();

        moved.ScheduledStartUtc.Should().Be(newStart);
        moved.ScheduledEndUtc.Should().BeAfter(newStart);
        moved.Status.Should().Be(AppointmentStatus.Pending);
        moved.History.Should().HaveCount(2);
        moved.History[^1].Reason.Should().Contain("Termin pomjeren").And.Contain("Službeni put.");

        _factory.Events.Published.OfType<AppointmentRescheduledEvent>()
            .Should().ContainSingle(e => e.AppointmentId == booked.Id && e.OldStartUtc == oldStart && e.NewStartUtc == newStart);

        // Staff can move it too, and a cancelled appointment cannot be moved by anyone.
        (await _client.SendJsonAsync(
                HttpMethod.Put, url, new AppointmentRescheduleRequest { NewStartUtc = newStart.AddHours(1) }, admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await CancelAsync($"/api/appointments/{booked.Id}", admin.AccessToken, "Otkazano radi testa.");

        (await _client.SendJsonAsync(
                HttpMethod.Put, url, new AppointmentRescheduleRequest { NewStartUtc = newStart }, admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Lists_AreScopedToTheCaller_Searchable_AndSummarisedByStatus()
    {
        var customer = await NewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var serviceId = customer.Services[0].Id;

        var first = await _client.BookAsync(customer.Token, customer.Vehicle.Id, TestData.Slot(8, 8), serviceId);
        var second = await _client.BookAsync(customer.Token, customer.Vehicle.Id, TestData.Slot(8, 13), serviceId);
        await CancelAsync($"/api/appointments/{second.Id}", customer.Token, "Jedan termin je dovoljan.");

        var mine = await (await _client.GetAsync("/api/appointments", customer.Token))
            .ReadAsync<PagedResult<AppointmentListItemResponse>>();

        mine.TotalCount.Should().Be(2);
        mine.Items.Select(a => a.ScheduledStartUtc).Should().BeInDescendingOrder();
        mine.Items.Should().OnlyContain(a => a.ServiceNames.Count == 1 && a.VehicleId == customer.Vehicle.Id);

        var pendingOnly = await (await _client.GetAsync("/api/appointments?status=Pending", customer.Token))
            .ReadAsync<PagedResult<AppointmentListItemResponse>>();
        pendingOnly.Items.Should().ContainSingle().Which.Id.Should().Be(first.Id);

        var summary = await (await _client.GetAsync("/api/appointments/summary", customer.Token))
            .ReadAsync<AppointmentSummaryResponse>();
        summary.Should().BeEquivalentTo(new { Total = 2, Pending = 1, Cancelled = 1, Completed = 0, Active = 1 });

        // Staff see everyone's and can find one by its number.
        var found = await (await _client.GetAsync(
                $"/api/appointments?search={first.AppointmentNumber}&pageSize=100000", admin.AccessToken))
            .ReadAsync<PagedResult<AppointmentListItemResponse>>();

        found.PageSize.Should().Be(PagingLimits.MaxPageSize);
        found.Items.Should().ContainSingle().Which.Id.Should().Be(first.Id);

        (await (await _client.GetAsync("/api/appointments/summary", admin.AccessToken))
            .ReadAsync<AppointmentSummaryResponse>()).Total.Should().BeGreaterThan(2);
    }

    private async Task<TestCustomer> NewCustomerAsync()
    {
        var (auth, _) = await _client.RegisterNewCustomerAsync();
        var vehicle = await _client.CreateVehicleAsync(auth.AccessToken);
        var services = await _client.GetServicesForAsync(vehicle.EngineTypeId, auth.AccessToken);

        return new TestCustomer(auth, auth.AccessToken, vehicle, services);
    }

    private async Task<(HttpStatusCode Status, string? Field)> TryBookAsync(
        string accessToken,
        int vehicleId,
        DateTime start,
        params int[] serviceIds)
    {
        var response = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/appointments",
            new AppointmentCreateRequest { VehicleId = vehicleId, ServiceIds = serviceIds, ScheduledStartUtc = start },
            accessToken);

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return (response.StatusCode, null);
        }

        var error = await response.ReadAsync<ApiErrorResponse>();
        return (response.StatusCode, error.Errors?.Keys.FirstOrDefault());
    }

    private Task<HttpResponseMessage> CancelAsync(string appointmentUrl, string accessToken, string reason) =>
        _client.SendJsonAsync(
            HttpMethod.Post, $"{appointmentUrl}/cancel", new AppointmentCancelRequest { Reason = reason }, accessToken);

    private sealed record TestCustomer(
        AuthResponse Auth,
        string Token,
        VehicleResponse Vehicle,
        IReadOnlyList<ServiceResponse> Services);
}
