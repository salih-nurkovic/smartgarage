using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A stored behavioural signal linking a customer to a service.
/// <para>
/// This table is what lets the recommender honestly claim to use stored data. The instructions
/// are blunt about it: "podaci koji ulaze u recommender, kao sto je historija pretraga i slicno,
/// moraju se stvarno upisivati u aplikaciji", and separately that a signal which is collected
/// must actually affect scoring. Every row written here is read by the scorer.
/// </para>
/// <para>
/// Rows are written at the moment the action happens - the catalogue service writes Viewed, the
/// appointment service writes Booked, Completed and Cancelled, the review service writes
/// Reviewed. Nothing is fabricated at recommendation time.
/// </para>
/// </summary>
public class UserServiceSignal : BaseEntity
{
    public int UserId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>The vehicle in context, when the action had one.</summary>
    public int? VehicleId { get; set; }

    public SignalType SignalType { get; set; }

    /// <summary>
    /// Signed weight from configuration. Positive for interest, negative for rejection - a
    /// dismissed recommendation or a one-star review pushes the service away rather than
    /// counting as engagement.
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// When the action happened. Scoring applies exponential time decay from here, so a service
    /// booked three years ago counts for far less than one booked last month.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>
    /// Set by a MarkedDone signal: the customer says the work was already done elsewhere, on
    /// this date. Restarts the interval clock without inventing an appointment.
    /// </summary>
    public DateTime? PerformedAtUtc { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Vehicle? Vehicle { get; set; }
}
