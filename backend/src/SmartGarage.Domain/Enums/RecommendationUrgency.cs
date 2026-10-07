namespace SmartGarage.Domain.Enums;

/// <summary>
/// Urgency badge shown on each recommendation card. Taken from the approved mockups, which
/// label every recommendation either "Planirano" or "Hitno".
/// </summary>
public enum RecommendationUrgency
{
    /// <summary>Approaching its recommended interval. Shown as "Planirano".</summary>
    Planned = 1,

    /// <summary>Past its recommended interval by mileage or by age. Shown as "Hitno".</summary>
    Urgent = 2,
}
