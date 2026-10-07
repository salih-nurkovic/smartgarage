using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities.Reference;

/// <summary>
/// Reference entity. A model always belongs to a brand, which is what makes the model dropdown
/// on the vehicle form depend on the brand chosen above it.
/// </summary>
public class VehicleModel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public bool IsActive { get; set; } = true;

    public VehicleBrand Brand { get; set; } = null!;
    public ICollection<Vehicle> Vehicles { get; set; } = [];
}
