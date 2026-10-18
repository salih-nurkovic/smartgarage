using FluentAssertions;
using FluentValidation.Results;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Application.Dtos.Payments;
using SmartGarage.Application.Dtos.Reviews;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Application.Validators;
using Xunit;

namespace SmartGarage.UnitTests.Validators;

/// <summary>
/// The server-side half of form validation: each refusal is tied to the field it belongs to,
/// so a client can show the message under that control, and states the rule in plain words.
/// </summary>
public sealed class RequestValidatorTests
{
    [Fact]
    public void Booking_NeedsAtLeastOneService_NoRepeats_AndNoMoreThanTen()
    {
        var validator = new AppointmentCreateRequestValidator();
        var valid = new AppointmentCreateRequest { VehicleId = 1, ServiceIds = [1, 2], ScheduledStartUtc = DateTime.UtcNow };

        validator.Validate(valid).IsValid.Should().BeTrue();
        Fails(validator.Validate(new AppointmentCreateRequest { VehicleId = 1, ScheduledStartUtc = DateTime.UtcNow }),
            nameof(AppointmentCreateRequest.ServiceIds), "najmanje jednu");
        Fails(validator.Validate(new AppointmentCreateRequest { VehicleId = 1, ServiceIds = [3, 3], ScheduledStartUtc = DateTime.UtcNow }),
            nameof(AppointmentCreateRequest.ServiceIds), "više puta");
        Fails(validator.Validate(new AppointmentCreateRequest { VehicleId = 1, ServiceIds = [.. Enumerable.Range(1, 11)], ScheduledStartUtc = DateTime.UtcNow }),
            nameof(AppointmentCreateRequest.ServiceIds), "najviše 10");
        Fails(validator.Validate(new AppointmentCreateRequest { ServiceIds = [1], ScheduledStartUtc = DateTime.UtcNow }),
            nameof(AppointmentCreateRequest.VehicleId), "vozilo");
    }

    [Theory]
    [InlineData("", "obavezno")]
    [InlineData("   ", "obavezno")]
    [InlineData("Ne", "između 5 i 500")]
    public void Cancellation_IsRefusedWithoutARealReason(string reason, string fragment) =>
        Fails(new AppointmentCancelRequestValidator().Validate(new AppointmentCancelRequest { Reason = reason }),
            nameof(AppointmentCancelRequest.Reason), fragment);

    [Theory]
    [InlineData(null, true)]        // no amount: everything still refundable
    [InlineData(10.5, true)]
    [InlineData(0.0, false)]
    [InlineData(-5.0, false)]
    [InlineData(10.555, false)]     // a third decimal cannot be charged back
    public void Refund_AmountIsPositive_WithAtMostTwoDecimals(double? amount, bool valid)
    {
        var request = new RefundRequest { Amount = (decimal?)amount, Reason = "Dogovoreni povrat." };

        new RefundRequestValidator().Validate(request).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Refund_NeedsAReason() =>
        Fails(new RefundRequestValidator().Validate(new RefundRequest { Amount = 10 }),
            nameof(RefundRequest.Reason), "obavezno");

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Review_RatingIsOneToFive(int rating, bool valid) =>
        new ReviewCreateRequestValidator()
            .Validate(new ReviewCreateRequest { AppointmentId = 1, ServiceId = 1, Rating = rating })
            .IsValid.Should().Be(valid);

    [Fact]
    public void Service_DurationGoesInFiveMinuteSteps_AndTheAgeRangeIsNotUpsideDown()
    {
        var validator = new ServiceRequestValidator();

        validator.Validate(Service()).IsValid.Should().BeTrue();
        Fails(validator.Validate(Service(duration: 62)), nameof(ServiceRequest.EstimatedDurationMinutes), "koracima od 5");
        Fails(validator.Validate(Service(duration: 2000)), nameof(ServiceRequest.EstimatedDurationMinutes), "između 5 i 1440");
        Fails(validator.Validate(Service(price: 10.999m)), nameof(ServiceRequest.Price), "dvije decimale");
        Fails(validator.Validate(Service(price: 0)), nameof(ServiceRequest.Price), "Cijena mora biti");
        Fails(validator.Validate(Service(minAge: 10, maxAge: 5)), nameof(ServiceRequest.MaxVehicleAgeYears), "manja od najmanje");
    }

    [Theory]
    [InlineData("WVWZZZ1KZAW000001", true)]
    [InlineData("wvwzzz1kzaw000001", true)]     // typed in lower case
    [InlineData("WVWZZZ1KZAW00000", false)]     // 16 characters
    [InlineData("WVWZZZ1KZAWO00001", false)]    // the letter O is never used in a VIN
    [InlineData("WVWZZZ1KZAWI00001", false)]
    public void Vehicle_VinFollowsTheStandard(string vin, bool valid) =>
        new VehicleRequestValidator(new FixedYear(2026)).Validate(Vehicle(vin: vin)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(1949, false)]
    [InlineData(1950, true)]
    [InlineData(2027, true)]    // next year's models are already sold
    [InlineData(2028, false)]
    public void Vehicle_YearIsBetween1950AndNextYear(int year, bool valid) =>
        new VehicleRequestValidator(new FixedYear(2026)).Validate(Vehicle(year: year)).IsValid.Should().Be(valid);

    private static void Fails(ValidationResult result, string property, string fragment) =>
        result.Errors.Should().Contain(
            e => e.PropertyName == property && e.ErrorMessage.Contains(fragment),
            $"{property} should be refused with a message mentioning \"{fragment}\"");

    private static ServiceRequest Service(int duration = 60, decimal price = 49.90m, int? minAge = null, int? maxAge = null) =>
        new()
        {
            Name = "Mali servis",
            Description = "Zamjena ulja i filtera.",
            CategoryId = 1,
            Price = price,
            EstimatedDurationMinutes = duration,
            MinVehicleAgeYears = minAge,
            MaxVehicleAgeYears = maxAge,
        };

    private static VehicleRequest Vehicle(string vin = "WVWZZZ1KZAW000001", int year = 2015) =>
        new()
        {
            BrandId = 1,
            ModelId = 1,
            EngineTypeId = 1,
            ProductionYear = year,
            RegistrationPlate = "A12-K-345",
            Vin = vin,
            CurrentMileageKm = 150_000,
        };

    private sealed class FixedYear(int year) : IDateTimeProvider
    {
        public DateTime UtcNow => new(year, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
    }
}
