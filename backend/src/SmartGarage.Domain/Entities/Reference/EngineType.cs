using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities.Reference;

/// <summary>
/// Reference entity. Diesel, petrol, hybrid, electric, LPG.
/// <para>
/// This is also a recommender feature in its own right: it is both a hard filter (a petrol car
/// is never offered diesel particulate filter cleaning) and a scoring boost.
/// </para>
/// </summary>
public class EngineType : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable short code used by the recommender's attribute-to-tag mapping.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = [];
    public ICollection<ServiceEngineTypeLink> ServiceLinks { get; set; } = [];
}
