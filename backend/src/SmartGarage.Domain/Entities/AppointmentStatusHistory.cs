using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Immutable audit trail of every status change.
/// <para>
/// This is the entity that answers the instruction "audit trag: ko je odobrio ili odbio zahtjev,
/// kada je to ucinjeno i odgovarajuci opis unutar zapisa". Rows are only ever inserted, never
/// updated or deleted.
/// </para>
/// </summary>
public class AppointmentStatusHistory : BaseEntity
{
    public int AppointmentId { get; set; }

    /// <summary>Null on the very first row, which records creation.</summary>
    public AppointmentStatus? FromStatus { get; set; }

    public AppointmentStatus ToStatus { get; set; }

    public int ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }

    /// <summary>Mandatory for cancellations and reschedules; optional elsewhere.</summary>
    public string? Reason { get; set; }

    /// <summary>Role the actor held at the time, so the trail stays readable after a role change.</summary>
    public string ActorRole { get; set; } = string.Empty;

    public Appointment Appointment { get; set; } = null!;
    public ApplicationUser ChangedByUser { get; set; } = null!;
}
