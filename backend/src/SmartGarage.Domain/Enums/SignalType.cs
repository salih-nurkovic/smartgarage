namespace SmartGarage.Domain.Enums;

/// <summary>
/// Behavioural signals stored for the recommender. The instructions are explicit that a signal
/// which is collected must actually be used in scoring, so every value here is read by the
/// scorer - none is recorded and then ignored.
/// </summary>
public enum SignalType
{
    /// <summary>Customer opened a service detail screen.</summary>
    Viewed = 1,

    /// <summary>Service appeared in a search the customer ran.</summary>
    Searched = 2,

    /// <summary>Service was put on an appointment.</summary>
    Booked = 3,

    /// <summary>Appointment containing this service reached Completed.</summary>
    Completed = 4,

    /// <summary>Customer left a review for this service.</summary>
    Reviewed = 5,

    /// <summary>Appointment containing this service was cancelled.</summary>
    Cancelled = 6,

    /// <summary>Audit only: the recommendation was displayed.</summary>
    RecommendationShown = 7,

    /// <summary>Customer tapped a recommendation.</summary>
    RecommendationClicked = 8,

    /// <summary>Customer dismissed a recommendation.</summary>
    RecommendationDismissed = 9,

    /// <summary>
    /// Customer pressed "Obavljeno": the work was already done elsewhere. Suppresses the
    /// recommendation and restarts the interval clock for that service on that vehicle.
    /// </summary>
    MarkedDone = 10,
}
