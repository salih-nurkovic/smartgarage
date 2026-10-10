using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Auth;
using SmartGarage.Application.Dtos.Employees;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Employees;

[Collection(ApiTestGroup.Name)]
public sealed class EmployeeTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeeTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Employees_AreVisibleToStaff_HiddenFromCustomers_AndManagedOnlyByAdministrators()
    {
        var customer = await _client.LoginAsync("customer");
        var employee = await _client.LoginAsync("employee");

        (await _client.GetAsync("/api/employees", customer.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var list = await (await _client.GetAsync("/api/employees?search=mehani", employee.AccessToken))
            .ReadAsync<PagedResult<EmployeeResponse>>();

        list.Items.Should().NotBeEmpty();
        list.Items.Should().OnlyContain(e => e.Schedule.Count == 7);

        (await _client.SendJsonAsync(HttpMethod.Post, "/api/employees", NewEmployee(), employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.DeleteAsync($"/api/employees/{list.Items[0].Id}", employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NewEmployee_GetsAnAccountWithTheEmployeeRole_AndAFullWorkingWeek()
    {
        var admin = await _client.LoginAsync("admin");
        var request = NewEmployee();

        var response = await _client.SendJsonAsync(HttpMethod.Post, "/api/employees", request, admin.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.ReadAsync<EmployeeResponse>();
        created.Position.Should().Be(request.Position);
        created.CompletedJobs.Should().Be(0);
        created.AverageRating.Should().BeNull();
        created.Schedule.Should().HaveCount(7);
        created.Schedule.Where(d => d.IsWorkingDay).Select(d => d.DayOfWeek).Should().BeEquivalentTo(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

        // The account works straight away and carries the role nothing else can grant.
        var login = await _client.LoginAsync(request.UserName, ApiClientExtensions.StrongPassword);
        login.User.Roles.Should().Equal(Roles.Employee);

        var updated = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"/api/employees/{created.Id}",
            new EmployeeUpdateRequest
            {
                Email = request.Email,
                FirstName = "Izmijenjen",
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                Position = "Glavni dijagnostičar",
                HireDate = request.HireDate,
                IsAvailableForAssignment = false,
            },
            admin.AccessToken);

        var edited = await updated.ReadAsync<EmployeeResponse>();
        edited.FirstName.Should().Be("Izmijenjen");
        edited.Position.Should().Be("Glavni dijagnostičar");
        edited.Specialization.Should().BeNull();
        edited.IsAvailableForAssignment.Should().BeFalse();
    }

    [Fact]
    public async Task Schedule_MustCoverTheWholeWeek_WithEveryDayEndingAfterItStarts()
    {
        var admin = await _client.LoginAsync("admin");
        var created = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/employees", NewEmployee(), admin.AccessToken))
            .ReadAsync<EmployeeResponse>();
        var url = $"/api/employees/{created.Id}/schedule";

        var tooFewDays = await _client.SendJsonAsync(
            HttpMethod.Put, url, new EmployeeScheduleRequest { Days = [Day(DayOfWeek.Monday, 8, 16)] }, admin.AccessToken);
        tooFewDays.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooFewDays.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("days");

        var backwards = Week(8, 16);
        backwards[2] = Day(backwards[2].DayOfWeek, 16, 8);

        (await _client.SendJsonAsync(
                HttpMethod.Put, url, new EmployeeScheduleRequest { Days = backwards }, admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var saved = await _client.SendJsonAsync(
            HttpMethod.Put, url, new EmployeeScheduleRequest { Days = Week(9, 15) }, admin.AccessToken);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);

        var schedule = (await saved.ReadAsync<EmployeeResponse>()).Schedule;
        schedule.Should().HaveCount(7);
        schedule.Should().OnlyContain(d => d.StartTime == new TimeOnly(9, 0) && d.EndTime == new TimeOnly(15, 0));
        schedule.Single(d => d.DayOfWeek == DayOfWeek.Saturday).IsWorkingDay.Should().BeTrue();
    }

    [Fact]
    public async Task Employee_WithoutJobsCanBeRemoved_ButOneWithAssignedAppointmentsCannot()
    {
        var admin = await _client.LoginAsync("admin");

        var idle = NewEmployee();
        var idleProfile = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/employees", idle, admin.AccessToken))
            .ReadAsync<EmployeeResponse>();
        var idleSession = await _client.LoginAsync(idle.UserName, ApiClientExtensions.StrongPassword);

        var busyProfile = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/employees", NewEmployee(), admin.AccessToken))
            .ReadAsync<EmployeeResponse>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();
            var customerId = await db.Users.Where(u => u.UserName == "amar").Select(u => u.Id).SingleAsync();
            var start = DateTime.UtcNow.AddDays(-10);

            var vehicle = new Vehicle
            {
                OwnerId = customerId,
                BrandId = 1,
                ModelId = 1,
                EngineTypeId = 1,
                ProductionYear = 2018,
                RegistrationPlate = $"E{Guid.NewGuid():N}"[..9].ToUpperInvariant(),
                Vin = Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
                CurrentMileageKm = 100_000,
                LastMileageUpdatedAtUtc = start,
            };

            db.Appointments.Add(new Appointment
            {
                AppointmentNumber = $"APP-E-{Guid.NewGuid():N}"[..20],
                CustomerId = customerId,
                Vehicle = vehicle,
                AssignedEmployeeId = busyProfile.Id,
                ScheduledStartUtc = start,
                ScheduledEndUtc = start.AddHours(1),
                Status = AppointmentStatus.Completed,
                PaymentStatus = AppointmentPaymentStatus.Unpaid,
                CompletedAtUtc = start.AddHours(1),
                TotalPrice = 40m,
            });

            await db.SaveChangesAsync();
        }

        var blocked = await _client.DeleteAsync($"/api/employees/{busyProfile.Id}", admin.AccessToken);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.ReadAsync<ApiErrorResponse>()).Detail.Should().Contain("1 rezervacija");

        (await (await _client.GetAsync($"/api/employees/{busyProfile.Id}", admin.AccessToken))
            .ReadAsync<EmployeeResponse>()).CompletedJobs.Should().Be(1);

        (await _client.DeleteAsync($"/api/employees/{idleProfile.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/employees/{idleProfile.Id}", admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The removed employee is signed out at once and cannot sign back in.
        (await _client.GetAsync("/api/profile", idleSession.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest { UserName = idle.UserName, Password = ApiClientExtensions.StrongPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Employee_WithInvalidData_IsRejectedFieldByField()
    {
        var admin = await _client.LoginAsync("admin");

        var response = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/employees",
            new EmployeeCreateRequest
            {
                UserName = "employee",
                Email = "nije-email",
                FirstName = "A",
                LastName = "Prezime",
                PhoneNumber = "+387 61 111 111",
                Password = "slaba",
                ConfirmPassword = "slaba",
                Position = string.Empty,
                HireDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            },
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ApiErrorResponse>()).Errors!.Keys.Should().BeEquivalentTo(
            "email", "firstName", "password", "position", "hireDate");

        var takenUserName = NewEmployee();
        var duplicate = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/employees",
            new { userName = "employee", takenUserName.Email, takenUserName.FirstName, takenUserName.LastName, takenUserName.PhoneNumber, takenUserName.Password, takenUserName.ConfirmPassword, takenUserName.Position, hireDate = "2020-01-15" },
            admin.AccessToken);

        (await duplicate.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("userName");
    }

    private static EmployeeCreateRequest NewEmployee()
    {
        var userName = $"radnik{Guid.NewGuid():N}"[..20];

        return new EmployeeCreateRequest
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            FirstName = "Novi",
            LastName = "Mehaničar",
            PhoneNumber = "+387 61 444 555",
            Password = ApiClientExtensions.StrongPassword,
            ConfirmPassword = ApiClientExtensions.StrongPassword,
            Position = "Mehaničar",
            Specialization = "Kočioni sistemi",
            HireDate = new DateOnly(2021, 3, 1),
        };
    }

    private static EmployeeScheduleDay Day(DayOfWeek day, int startHour, int endHour) => new()
    {
        DayOfWeek = day,
        StartTime = new TimeOnly(startHour, 0),
        EndTime = new TimeOnly(endHour, 0),
        IsWorkingDay = true,
    };

    private static List<EmployeeScheduleDay> Week(int startHour, int endHour) =>
        Enum.GetValues<DayOfWeek>().Select(day => Day(day, startHour, endHour)).ToList();
}
