using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Every recommendation ever served, with its score and the explanation shown to the customer.
/// <para>
/// Two reasons this is persisted rather than computed and forgotten. It makes the recommender
/// auditable - the explanation the customer saw can be reproduced exactly. And it is the
/// evidence that recommendations genuinely change after new service history, which the
/// recommender documentation is required to demonstrate.
/// </para>
/// </summary>
public class RecommendationLog : BaseEntity
{
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Final score after similarity, boosts and penalties.</summary>
    public double Score { get; set; }

    /// <summary>Position in the served list, 1 being first.</summary>
    public int Rank { get; set; }

    /// <summary>Urgency badge shown on the card: Planirano or Hitno.</summary>
    public RecommendationUrgency Urgency { get; set; }

    /// <summary>The sentence shown to the customer.</summary>
    public string ExplanationText { get; set; } = string.Empty;

    /// <summary>
    /// The scored factors and their individual contributions, as JSON. This is the part that
    /// makes the explanation verifiable rather than decorative.
    /// </summary>
    public string ExplanationFactorsJson { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>Which version of the algorithm produced this, from configuration.</summary>
    public string AlgorithmVersion { get; set; } = string.Empty;

    public bool WasClicked { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
