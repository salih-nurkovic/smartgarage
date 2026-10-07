using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities.Reference;

/// <summary>
/// Reference entity. Groups the catalogue and forms one block of the recommender feature
/// vector, so a customer who keeps booking "Redovno odrzavanje" is offered more of it.
/// </summary>
public class ServiceCategory : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconPath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public ICollection<Service> Services { get; set; } = [];
}
