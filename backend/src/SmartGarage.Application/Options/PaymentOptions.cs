namespace SmartGarage.Application.Options;

/// <summary>The currency Stripe charges in, from <c>.env</c>.</summary>
public sealed class PaymentOptions
{
    public string StripeCurrency { get; set; } = string.Empty;
}
