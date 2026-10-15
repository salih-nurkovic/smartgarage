using FluentValidation;
using SmartGarage.Application.Dtos.Appointments;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

public sealed class AppointmentCreateRequestValidator : AbstractValidator<AppointmentCreateRequest>
{
    public AppointmentCreateRequestValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0).WithMessage("Izaberite vozilo.");

        RuleFor(x => x.ServiceIds)
            .NotEmpty().WithMessage("Izaberite najmanje jednu uslugu.")
            .Must(ids => ids.Count <= ValidationRules.MaxServicesPerAppointment)
            .WithMessage($"Jedna rezervacija može imati najviše {ValidationRules.MaxServicesPerAppointment} usluga.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Ista usluga je izabrana više puta.");

        RuleFor(x => x.ScheduledStartUtc).NotEmpty().WithMessage("Izaberite datum i vrijeme termina.");

        RuleFor(x => x.CustomerNotes)
            .MaximumLength(ValidationRules.CustomerNotesMaxLength)
            .WithMessage($"Napomena može imati najviše {ValidationRules.CustomerNotesMaxLength} znakova.");
    }
}

public sealed class AppointmentCancelRequestValidator : AbstractValidator<AppointmentCancelRequest>
{
    public AppointmentCancelRequestValidator()
    {
        RuleFor(x => x.Reason)
            .RequiredText("Razlog otkazivanja", ValidationRules.ReasonMinLength, ValidationRules.ReasonMaxLength);
    }
}

public sealed class AppointmentCompleteRequestValidator : AbstractValidator<AppointmentCompleteRequest>
{
    public AppointmentCompleteRequestValidator()
    {
        RuleFor(x => x.MileageAtAppointmentKm).Mileage();

        RuleFor(x => x.InternalNotes)
            .MaximumLength(ValidationRules.InternalNotesMaxLength)
            .WithMessage($"Interna napomena može imati najviše {ValidationRules.InternalNotesMaxLength} znakova.");
    }
}

public sealed class AppointmentRescheduleRequestValidator : AbstractValidator<AppointmentRescheduleRequest>
{
    // The history row also holds the old and the new time, which need room of their own.
    private const int ReasonRoom = 400;

    public AppointmentRescheduleRequestValidator()
    {
        RuleFor(x => x.NewStartUtc).NotEmpty().WithMessage("Izaberite novi datum i vrijeme termina.");

        RuleFor(x => x.Reason)
            .MaximumLength(ReasonRoom)
            .WithMessage($"Razlog može imati najviše {ReasonRoom} znakova.");
    }
}

public sealed class AppointmentAssignRequestValidator : AbstractValidator<AppointmentAssignRequest>
{
    public AppointmentAssignRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0).WithMessage("Izaberite mehaničara.");
    }
}
