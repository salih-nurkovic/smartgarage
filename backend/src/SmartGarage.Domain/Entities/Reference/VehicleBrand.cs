using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities.Reference;

/// <summary>
/// Reference entity. Vehicle manufacturers, managed by an administrator through a CRUD form
/// and offered to the customer as a dropdown rather than a free text box.
/// </summary>
public class VehicleBrand : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<VehicleModel> Models { get; set; } = [];
}
