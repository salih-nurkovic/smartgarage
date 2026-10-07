using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Joins a service to a descriptive tag, carrying a weight.
/// <para>
/// The weight is why this is a real entity rather than a plain many-to-many table: a service
/// can be strongly or weakly associated with a tag, and that strength feeds straight into the
/// item vector the recommender scores against.
/// </para>
/// </summary>
public class ServiceTagLink
{
    public int ServiceId { get; set; }
    public int ServiceTagId { get; set; }

    /// <summary>Association strength, 0 to 1. Multiplied by the tag's IDF weight when scoring.</summary>
    public double Weight { get; set; } = 1.0;

    public Service Service { get; set; } = null!;
    public ServiceTag ServiceTag { get; set; } = null!;
}
