using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Employees;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

public sealed class EmployeeCreateRequestValidator : AbstractValidator<EmployeeCreateRequest>
{
    public EmployeeCreateRequestValidator(IOptions<IdentityOptions> identity, IDateTimeProvider clock)
    {
        RuleFor(x => x.UserName).UserNameFormat();
        RuleFor(x => x.Email).EmailFormat();
        RuleFor(x => x.FirstName).PersonName("Ime");
        RuleFor(x => x.LastName).PersonName("Prezime");
        RuleFor(x => x.PhoneNumber).PhoneFormat();
        RuleFor(x => x.Password).StrongPassword(identity.Value.Password);
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Lozinke se ne podudaraju.");
        RuleFor(x => x.Position).Position();
        RuleFor(x => x.Specialization).Specialization();
        RuleFor(x => x.HireDate).HireDate(clock);
    }
}

public sealed class EmployeeUpdateRequestValidator : AbstractValidator<EmployeeUpdateRequest>
{
    public EmployeeUpdateRequestValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.Email).EmailFormat();
        RuleFor(x => x.FirstName).PersonName("Ime");
        RuleFor(x => x.LastName).PersonName("Prezime");
        RuleFor(x => x.PhoneNumber).PhoneFormat();
        RuleFor(x => x.Position).Position();
        RuleFor(x => x.Specialization).Specialization();
        RuleFor(x => x.HireDate).HireDate(clock);
    }
}

public sealed class EmployeeScheduleRequestValidator : AbstractValidator<EmployeeScheduleRequest>
{
    private const int DaysInWeek = 7;

    public EmployeeScheduleRequestValidator()
    {
        RuleFor(x => x.Days)
            .Must(days => days.Count == DaysInWeek
                          && days.Select(d => d.DayOfWeek).Distinct().Count() == DaysInWeek
                          && days.All(d => Enum.IsDefined(d.DayOfWeek)))
            .WithMessage("Raspored mora sadržavati svih sedam dana u sedmici, svaki tačno jednom.");

        RuleForEach(x => x.Days)
            .Must(day => day.EndTime > day.StartTime)
            .WithMessage("Kraj radnog vremena mora biti nakon početka.");
    }
}

internal static class EmployeeRules
{
    public static IRuleBuilderOptions<T, string> Position<T>(this IRuleBuilder<T, string> rule) =>
        rule.RequiredText("Radno mjesto", ValidationRules.NameMinLength, ValidationRules.PositionMaxLength);

    public static IRuleBuilderOptions<T, string?> Specialization<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .MaximumLength(ValidationRules.SpecializationMaxLength)
            .WithMessage($"Specijalnost može imati najviše {ValidationRules.SpecializationMaxLength} znakova.");

    public static IRuleBuilderOptions<T, DateOnly> HireDate<T>(
        this IRuleBuilder<T, DateOnly> rule,
        IDateTimeProvider clock) =>
        rule
            .Must(date => date.Year >= ValidationRules.HireYearMin && date <= clock.TodayUtc)
            .WithMessage(
                $"Datum zaposlenja mora biti između {ValidationRules.HireYearMin}. godine i današnjeg dana.");
}
