using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Which engine types a service applies to.
/// <para>
/// Acts as a hard filter before scoring: a petrol car is never recommended diesel particulate
/// filter cleaning, no matter how well the rest of the vector matches.
/// </para>
/// </summary>
public class ServiceEngineTypeLink
{
    public int ServiceId { get; set; }
    public int EngineTypeId { get; set; }

    public Service Service { get; set; } = null!;
    public EngineType EngineType { get; set; } = null!;
}
