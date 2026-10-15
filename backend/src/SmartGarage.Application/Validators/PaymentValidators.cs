using FluentValidation;
using SmartGarage.Application.Dtos.Payments;
using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Validators;

public sealed class PaymentIntentRequestValidator : AbstractValidator<PaymentIntentRequest>
{
    public PaymentIntentRequestValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0).WithMessage("Izaberite rezervaciju koju plaćate.");
    }
}

public sealed class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    public RefundRequestValidator()
    {
        RuleFor(x => x.Reason)
            .RequiredText("Razlog povrata", ValidationRules.ReasonMinLength, ValidationRules.ReasonMaxLength);

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Iznos povrata mora biti veći od 0.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Iznos može imati najviše dvije decimale.");
    }
}
