using FluentValidation;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Application.Dtos.Reference;
using SmartGarage.Application.Dtos.Vehicles;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

public sealed class VehicleBrandRequestValidator : AbstractValidator<VehicleBrandRequest>
{
    public VehicleBrandRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText("Naziv marke", ValidationRules.NameMinLength, ValidationRules.BrandNameMaxLength);
    }
}

public sealed class VehicleModelRequestValidator : AbstractValidator<VehicleModelRequest>
{
    public VehicleModelRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText("Naziv modela", 1, ValidationRules.ModelNameMaxLength);
        RuleFor(x => x.BrandId).GreaterThan(0).WithMessage("Izaberite marku vozila.");
    }
}

public sealed class EngineTypeRequestValidator : AbstractValidator<EngineTypeRequest>
{
    public EngineTypeRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText("Naziv tipa motora", ValidationRules.NameMinLength, ValidationRules.EngineTypeNameMaxLength);
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Šifra je obavezno polje.")
            .Matches(ValidationRules.EngineTypeCode())
            .WithMessage("Šifra smije sadržavati 2 do 20 slova, brojeva ili donjih crta, npr. DIESEL.");
    }
}

public sealed class ServiceCategoryRequestValidator : AbstractValidator<ServiceCategoryRequest>
{
    public ServiceCategoryRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText("Naziv kategorije", ValidationRules.NameMinLength, ValidationRules.CategoryNameMaxLength);
        RuleFor(x => x.Description)
            .MaximumLength(ValidationRules.CategoryDescriptionMaxLength)
            .WithMessage($"Opis može imati najviše {ValidationRules.CategoryDescriptionMaxLength} znakova.");
        RuleFor(x => x.DisplayOrder)
            .InclusiveBetween(0, ValidationRules.DisplayOrderMax)
            .WithMessage($"Redoslijed mora biti broj između 0 i {ValidationRules.DisplayOrderMax}.");
    }
}

public sealed class ServiceTagRequestValidator : AbstractValidator<ServiceTagRequest>
{
    public ServiceTagRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText("Naziv oznake", ValidationRules.NameMinLength, ValidationRules.TagNameMaxLength);
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Identifikator je obavezno polje.")
            .MaximumLength(ValidationRules.TagNameMaxLength)
            .WithMessage($"Identifikator može imati najviše {ValidationRules.TagNameMaxLength} znakova.")
            .Matches(ValidationRules.Slug())
            .WithMessage("Identifikator smije sadržavati mala slova, brojeve i crtice, npr. high-mileage.");
    }
}

public sealed class ServiceRequestValidator : AbstractValidator<ServiceRequest>
{
    public ServiceRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText("Naziv usluge", ValidationRules.NameMinLength, ValidationRules.ServiceNameMaxLength);
        RuleFor(x => x.Description).RequiredText("Opis usluge", ValidationRules.ReasonMinLength, ValidationRules.ServiceDescriptionMaxLength);
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Izaberite kategoriju.");

        RuleFor(x => x.Price)
            .InclusiveBetween(ValidationRules.PriceMin, ValidationRules.PriceMax)
            .WithMessage($"Cijena mora biti između {ValidationRules.PriceMin} i {ValidationRules.PriceMax}.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Cijena može imati najviše dvije decimale.");

        RuleFor(x => x.EstimatedDurationMinutes)
            .InclusiveBetween(ValidationRules.DurationMinMinutes, ValidationRules.DurationMaxMinutes)
            .WithMessage(
                $"Trajanje mora biti između {ValidationRules.DurationMinMinutes} i "
                + $"{ValidationRules.DurationMaxMinutes} minuta.")
            .Must(minutes => minutes % ValidationRules.DurationStepMinutes == 0)
            .WithMessage($"Trajanje se unosi u koracima od {ValidationRules.DurationStepMinutes} minuta.");

        RuleFor(x => x.RecommendedMileageIntervalKm)
            .InclusiveBetween(ValidationRules.MileageIntervalMinKm, ValidationRules.MileageIntervalMaxKm)
            .WithMessage(
                $"Interval mora biti između {ValidationRules.MileageIntervalMinKm} i "
                + $"{ValidationRules.MileageIntervalMaxKm} km.");

        RuleFor(x => x.RecommendedMonthsInterval)
            .InclusiveBetween(1, ValidationRules.MonthsIntervalMax)
            .WithMessage($"Interval mora biti između 1 i {ValidationRules.MonthsIntervalMax} mjeseci.");

        RuleFor(x => x.MinVehicleAgeYears)
            .InclusiveBetween(0, ValidationRules.VehicleAgeMaxYears)
            .WithMessage($"Starost vozila mora biti između 0 i {ValidationRules.VehicleAgeMaxYears} godina.");

        RuleFor(x => x.MaxVehicleAgeYears)
            .InclusiveBetween(0, ValidationRules.VehicleAgeMaxYears)
            .WithMessage($"Starost vozila mora biti između 0 i {ValidationRules.VehicleAgeMaxYears} godina.")
            .GreaterThanOrEqualTo(x => x.MinVehicleAgeYears!.Value)
            .When(x => x.MinVehicleAgeYears.HasValue && x.MaxVehicleAgeYears.HasValue, ApplyConditionTo.CurrentValidator)
            .WithMessage("Najveća starost vozila ne može biti manja od najmanje.");
    }
}

public sealed class VehicleRequestValidator : AbstractValidator<VehicleRequest>
{
    public VehicleRequestValidator(IDateTimeProvider clock)
    {
        // Next year's models are sold before the year starts.
        var maxYear = clock.UtcNow.Year + 1;

        RuleFor(x => x.BrandId).GreaterThan(0).WithMessage("Izaberite marku vozila.");
        RuleFor(x => x.ModelId).GreaterThan(0).WithMessage("Izaberite model vozila.");
        RuleFor(x => x.EngineTypeId).GreaterThan(0).WithMessage("Izaberite tip motora.");

        RuleFor(x => x.ProductionYear)
            .InclusiveBetween(ValidationRules.VehicleYearMin, maxYear)
            .WithMessage($"Godina proizvodnje mora biti između {ValidationRules.VehicleYearMin} i {maxYear}.");

        RuleFor(x => x.RegistrationPlate)
            .RequiredText("Registarska oznaka", ValidationRules.PlateMinLength, ValidationRules.PlateMaxLength);

        RuleFor(x => x.Vin)
            .NotEmpty().WithMessage("Broj šasije (VIN) je obavezno polje.")
            .Must(vin => ValidationRules.Vin().IsMatch(vin.Trim().ToUpperInvariant()))
            .WithMessage(
                $"Broj šasije (VIN) ima tačno {ValidationRules.VinLength} znakova: slova i brojeve, "
                + "bez slova I, O i Q.");

        RuleFor(x => x.CurrentMileageKm).Mileage();
    }
}

public sealed class VehicleMileageRequestValidator : AbstractValidator<VehicleMileageRequest>
{
    public VehicleMileageRequestValidator()
    {
        RuleFor(x => x.CurrentMileageKm).Mileage();
    }
}
