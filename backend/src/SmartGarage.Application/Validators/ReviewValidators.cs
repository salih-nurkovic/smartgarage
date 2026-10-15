using FluentValidation;
using SmartGarage.Application.Dtos.Reviews;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

public sealed class ReviewCreateRequestValidator : AbstractValidator<ReviewCreateRequest>
{
    public ReviewCreateRequestValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0).WithMessage("Izaberite rezervaciju.");
        RuleFor(x => x.ServiceId).GreaterThan(0).WithMessage("Izaberite uslugu.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(ValidationRules.RatingMin, ValidationRules.RatingMax)
            .WithMessage($"Ocjena mora biti između {ValidationRules.RatingMin} i {ValidationRules.RatingMax}.");

        RuleFor(x => x.Comment)
            .MaximumLength(ValidationRules.ReviewCommentMaxLength)
            .WithMessage($"Komentar može imati najviše {ValidationRules.ReviewCommentMaxLength} znakova.");
    }
}
