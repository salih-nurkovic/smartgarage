using FluentAssertions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Application.Dtos.Employees;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Application.Services;

namespace SmartGarage.IntegrationTests.Support;

/// <summary>Builds the things most tests need before they can start: a vehicle, a slot, a booking.</summary>
public static class TestData
{
    public static async Task<VehicleResponse> CreateVehicleAsync(this HttpClient client, string accessToken)
    {
        var lookup = await (await client.GetAsync("/api/lookup", accessToken)).ReadAsync<LookupResponse>();
        var brand = lookup.Brands[0];

        var response = await client.SendJsonAsync(
            HttpMethod.Post,
            "/api/vehicles",
            new VehicleRequest
            {
                BrandId = brand.Id,
                ModelId = brand.Models[0].Id,
                EngineTypeId = lookup.EngineTypes[0].Id,
                ProductionYear = 2019,
                RegistrationPlate = $"T{Guid.NewGuid():N}"[..9].ToUpperInvariant(),

                // Hex digits are all valid VIN characters, so a GUID makes a unique, well-formed one.
                Vin = Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
                CurrentMileageKm = 85_000,
            },
            accessToken);

        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<VehicleResponse>();
    }

    /// <summary>Services that apply to the given engine type, so a booking is not refused for that.</summary>
    public static async Task<IReadOnlyList<ServiceResponse>> GetServicesForAsync(
        this HttpClient client,
        int engineTypeId,
        string accessToken) =>
        (await (await client.GetAsync(
                $"/api/services?engineTypeId={engineTypeId}&maxDurationMinutes=60&pageSize=100", accessToken))
            .ReadAsync<PagedResult<ServiceResponse>>()).Items;

    /// <summary>
    /// A bookable hour on the n-th upcoming Monday-to-Thursday, at least three days away so the
    /// customer cut-off does not interfere. Each test takes its own day, so tests sharing the
    /// database never compete for the same places.
    /// </summary>
    public static DateTime Slot(int dayIndex, int hour)
    {
        var day = DateTime.UtcNow.Date.AddDays(3);
        var remaining = dayIndex;

        while (true)
        {
            if (day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Thursday)
            {
                if (remaining == 0)
                {
                    return DateTime.SpecifyKind(day.AddHours(hour), DateTimeKind.Utc);
                }

                remaining--;
            }

            day = day.AddDays(1);
        }
    }

    public static async Task<AppointmentDetailResponse> BookAsync(
        this HttpClient client,
        string accessToken,
        int vehicleId,
        DateTime start,
        params int[] serviceIds)
    {
        var response = await client.SendJsonAsync(
            HttpMethod.Post,
            "/api/appointments",
            new AppointmentCreateRequest { VehicleId = vehicleId, ServiceIds = serviceIds, ScheduledStartUtc = start },
            accessToken);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadAsync<AppointmentDetailResponse>();
    }

    /// <summary>
    /// Books an appointment and takes it all the way to Completed, moving the test clock to the
    /// day of the appointment for as long as that takes.
    /// </summary>
    public static async Task<AppointmentDetailResponse> BookAndCompleteAsync(
        this HttpClient client,
        ApiFactory factory,
        string customerToken,
        VehicleResponse vehicle,
        DateTime start,
        int serviceId)
    {
        var admin = (await client.LoginAsync("admin")).AccessToken;

        var mechanic = (await (await client.GetAsync("/api/employees?search=Adnan", admin))
            .ReadAsync<PagedResult<EmployeeResponse>>()).Items.Single();

        var booked = await client.BookAsync(customerToken, vehicle.Id, start, serviceId);
        var url = $"/api/appointments/{booked.Id}";

        (await client.PostEmptyAsync($"{url}/confirm", admin)).EnsureSuccessStatusCode();
        (await client.SendJsonAsync(
            HttpMethod.Post, $"{url}/assign", new AppointmentAssignRequest { EmployeeId = mechanic.Id }, admin))
            .EnsureSuccessStatusCode();

        try
        {
            factory.Clock.Offset = start.AddMinutes(5) - DateTime.UtcNow;

            (await client.PostEmptyAsync($"{url}/start", admin)).EnsureSuccessStatusCode();

            var completed = await client.SendJsonAsync(
                HttpMethod.Post,
                $"{url}/complete",
                new AppointmentCompleteRequest { MileageAtAppointmentKm = vehicle.CurrentMileageKm + 500 },
                admin);

            completed.EnsureSuccessStatusCode();
            return await completed.ReadAsync<AppointmentDetailResponse>();
        }
        finally
        {
            factory.Clock.Offset = TimeSpan.Zero;
        }
    }

    public static Task<HttpResponseMessage> PostEmptyAsync(this HttpClient client, string url, string accessToken) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Post, url).WithBearer(accessToken));
}
