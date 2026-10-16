namespace SmartGarage.Application.Options;

/// <summary>
/// The garage's own details as the customer sees them, from <c>.env</c>: shown on the
/// appointment detail and used to label prices.
/// </summary>
public sealed class GarageOptions
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string DisplayCurrency { get; set; } = string.Empty;
}
