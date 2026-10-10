using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Users;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Application.Services;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.IntegrationTests.Support;
using Xunit;

namespace SmartGarage.IntegrationTests.Vehicles;

[Collection(ApiTestGroup.Name)]
public sealed class VehicleTests
{
    private static readonly byte[] JpegBytes =
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00];

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public VehicleTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Vehicle_BelongsToWhoeverCreatedIt_AndIsInvisibleToOtherCustomers()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();

        // ownerId in the body is ignored: there is no such field on the request.
        var body = await NewVehicleBodyAsync(owner.AccessToken);
        var created = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/vehicles",
            new { ownerId = stranger.User.Id, body.BrandId, body.ModelId, body.EngineTypeId, body.ProductionYear, body.RegistrationPlate, body.Vin, body.CurrentMileageKm },
            owner.AccessToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var vehicle = await created.ReadAsync<VehicleResponse>();

        vehicle.OwnerFullName.Should().Be($"{owner.User.FirstName} {owner.User.LastName}");
        vehicle.BrandName.Should().NotBeNullOrWhiteSpace();
        vehicle.Vin.Should().Be(body.Vin.ToUpperInvariant());

        (await GetPageAsync("/api/vehicles", owner.AccessToken)).Items.Should().ContainSingle();
        (await GetPageAsync("/api/vehicles", stranger.AccessToken)).Items.Should().BeEmpty();
        (await GetPageAsync($"/api/vehicles?ownerId={owner.User.Id}", stranger.AccessToken)).Items.Should().BeEmpty();

        var url = $"/api/vehicles/{vehicle.Id}";

        (await _client.GetAsync(url, stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendJsonAsync(HttpMethod.Put, url, body, stranger.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync(url, stranger.AccessToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await _client.GetAsync(url, owner.AccessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_CanFindAnyVehicle_ButOnlyAnAdministratorMayChangeOne()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var admin = await _client.LoginAsync("admin");
        var employee = await _client.LoginAsync("employee");

        var body = await NewVehicleBodyAsync(owner.AccessToken);
        var vehicle = await (await _client.SendJsonAsync(HttpMethod.Post, "/api/vehicles", body, owner.AccessToken))
            .ReadAsync<VehicleResponse>();
        var url = $"/api/vehicles/{vehicle.Id}";

        (await _client.SendJsonAsync(HttpMethod.Post, "/api/vehicles", body, admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var found = await GetPageAsync($"/api/vehicles?search={body.RegistrationPlate}", employee.AccessToken);
        found.Items.Should().ContainSingle().Which.Id.Should().Be(vehicle.Id);

        (await _client.SendJsonAsync(HttpMethod.Put, url, body, employee.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.DeleteAsync(url, employee.AccessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await _client.SendJsonAsync(HttpMethod.Put, url, body, admin.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Vehicle_IsValidatedAgainstTheDatabase_NotJustForFormat()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var body = await NewVehicleBodyAsync(owner.AccessToken);

        var vehicle = await (await _client.SendJsonAsync(HttpMethod.Post, "/api/vehicles", body, owner.AccessToken))
            .ReadAsync<VehicleResponse>();

        var duplicate = await _client.SendJsonAsync(HttpMethod.Post, "/api/vehicles", body, owner.AccessToken);
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await duplicate.ReadAsync<ApiErrorResponse>()).Errors!.Keys
            .Should().BeEquivalentTo("vin", "registrationPlate");

        var lookup = await (await _client.GetAsync("/api/lookup", owner.AccessToken)).ReadAsync<LookupResponse>();
        var otherBrand = lookup.Brands.First(b => b.Id != body.BrandId);

        var wrongModel = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/vehicles",
            new VehicleRequest
            {
                BrandId = otherBrand.Id,
                ModelId = body.ModelId,
                EngineTypeId = body.EngineTypeId,
                ProductionYear = 2020,
                RegistrationPlate = NewPlate(),
                Vin = NewVin(),
                CurrentMileageKm = 10,
            },
            owner.AccessToken);
        (await wrongModel.ReadAsync<ApiErrorResponse>()).Errors.Should().ContainKey("modelId");

        var malformed = await _client.SendJsonAsync(
            HttpMethod.Post,
            "/api/vehicles",
            new VehicleRequest
            {
                BrandId = body.BrandId,
                ModelId = body.ModelId,
                EngineTypeId = body.EngineTypeId,
                ProductionYear = 1900,
                RegistrationPlate = "A",
                Vin = "KRATAK-VIN",
                CurrentMileageKm = -5,
            },
            owner.AccessToken);
        (await malformed.ReadAsync<ApiErrorResponse>()).Errors!.Keys.Should().BeEquivalentTo(
            "productionYear", "registrationPlate", "vin", "currentMileageKm");

        // The odometer only goes forward.
        var url = $"/api/vehicles/{vehicle.Id}/mileage";

        var back = await _client.SendJsonAsync(
            HttpMethod.Patch, url, new VehicleMileageRequest { CurrentMileageKm = body.CurrentMileageKm - 1 }, owner.AccessToken);
        back.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var forward = await _client.SendJsonAsync(
            HttpMethod.Patch, url, new VehicleMileageRequest { CurrentMileageKm = body.CurrentMileageKm + 500 }, owner.AccessToken);
        (await forward.ReadAsync<VehicleResponse>()).CurrentMileageKm.Should().Be(body.CurrentMileageKm + 500);
    }

    [Fact]
    public async Task Vehicle_WithoutHistoryCanBeDeleted_ButOneWithAppointmentsCannot()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();

        var unused = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/vehicles", await NewVehicleBodyAsync(owner.AccessToken), owner.AccessToken))
            .ReadAsync<VehicleResponse>();
        var serviced = await (await _client.SendJsonAsync(
                HttpMethod.Post, "/api/vehicles", await NewVehicleBodyAsync(owner.AccessToken), owner.AccessToken))
            .ReadAsync<VehicleResponse>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartGarageDbContext>();
            var start = DateTime.UtcNow.AddDays(-30);

            db.Appointments.Add(new Appointment
            {
                AppointmentNumber = $"APP-T-{Guid.NewGuid():N}"[..20],
                CustomerId = owner.User.Id,
                VehicleId = serviced.Id,
                ScheduledStartUtc = start,
                ScheduledEndUtc = start.AddHours(1),
                Status = AppointmentStatus.Completed,
                PaymentStatus = AppointmentPaymentStatus.Unpaid,
                CompletedAtUtc = start.AddHours(1),
                TotalPrice = 50m,
            });

            await db.SaveChangesAsync();
        }

        (await _client.DeleteAsync($"/api/vehicles/{unused.Id}", owner.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/vehicles/{unused.Id}", owner.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var blocked = await _client.DeleteAsync($"/api/vehicles/{serviced.Id}", owner.AccessToken);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.ReadAsync<ApiErrorResponse>()).Detail.Should().Contain("1 rezervacija");

        var stillThere = await (await _client.GetAsync($"/api/vehicles/{serviced.Id}", owner.AccessToken))
            .ReadAsync<VehicleResponse>();
        stillThere.AppointmentCount.Should().Be(1);
        stillThere.LastServiceAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProfileImage_CanBeSetAndReadBack_OnlyByItsOwner()
    {
        var (owner, _) = await _client.RegisterNewCustomerAsync();
        var (stranger, _) = await _client.RegisterNewCustomerAsync();

        (await _client.GetAsync("/api/profile/image", owner.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var uploaded = await _client.PostFileAsync(
            "/api/profile/image", JpegBytes, "ja.jpg", "image/jpeg", owner.AccessToken);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await uploaded.ReadAsync<UserResponse>()).HasProfileImage.Should().BeTrue();

        var download = await _client.GetAsync("/api/profile/image", owner.AccessToken);
        download.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(JpegBytes);

        // The same URL for another user is that user's own picture - which does not exist.
        (await _client.GetAsync("/api/profile/image", stranger.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await _client.PostFileAsync("/api/profile/image", [], "prazna.jpg", "image/jpeg", owner.AccessToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<VehicleRequest> NewVehicleBodyAsync(string accessToken)
    {
        var lookup = await (await _client.GetAsync("/api/lookup", accessToken)).ReadAsync<LookupResponse>();
        var brand = lookup.Brands[0];

        return new VehicleRequest
        {
            BrandId = brand.Id,
            ModelId = brand.Models[0].Id,
            EngineTypeId = lookup.EngineTypes[0].Id,
            ProductionYear = 2019,
            RegistrationPlate = NewPlate(),
            Vin = NewVin(),
            CurrentMileageKm = 85_000,
        };
    }

    // Hex digits are all valid VIN characters, so a GUID makes a unique, well-formed one.
    private static string NewVin() => Guid.NewGuid().ToString("N")[..17].ToUpperInvariant();

    private static string NewPlate() => $"T{Guid.NewGuid():N}"[..9].ToUpperInvariant();

    private async Task<PagedResult<VehicleResponse>> GetPageAsync(string url, string accessToken)
    {
        var response = await _client.GetAsync(url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<PagedResult<VehicleResponse>>();
    }
}
