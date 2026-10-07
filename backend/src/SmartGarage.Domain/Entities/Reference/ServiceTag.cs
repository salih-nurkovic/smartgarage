using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities.Reference;

/// <summary>
/// Reference entity. Fine-grained descriptors such as diesel, high-mileage, winter, preventive.
/// <para>
/// These are the vocabulary of the content-based feature vector. Without them the recommender
/// would only have category to work with, which is too coarse to produce an explanation worth
/// reading. Tag weights are IDF-adjusted, so a tag carried by almost every service contributes
/// almost nothing while a rare one dominates.
/// </para>
/// </summary>
public class ServiceTag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceTagLink> ServiceLinks { get; set; } = [];
}
