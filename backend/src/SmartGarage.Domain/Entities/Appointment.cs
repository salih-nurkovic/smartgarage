using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// The core transactional entity: one visit of one vehicle to the garage.
/// <para>
/// <see cref="Status"/> is written in exactly one place in the whole codebase,
/// <c>AppointmentService.TransitionAsync</c>, which consults the state machine first. No
/// controller ever assigns it.
/// </para>
/// </summary>
public class Appointment : BaseEntity, IAuditable, ISoftDeletable
{
    /// <summary>
    /// Human-readable identifier such as APP-2026-000123, shown everywhere in both clients.
    /// The instructions forbid showing database IDs to users, but a customer still has to be
    /// able to name an appointment on the phone - this is what they say.
    /// </summary>
    public string AppointmentNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public int? AssignedEmployeeId { get; set; }

    public DateTime ScheduledStartUtc { get; set; }

    /// <summary>
    /// Computed on the server by walking the working-hours calendar, not by adding raw minutes,
    /// so a long job correctly spills into the next working day instead of running through the
    /// night. The instructions call out 25-hour and 48-hour durations as edge cases to handle.
    /// </summary>
    public DateTime ScheduledEndUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public AppointmentPaymentStatus PaymentStatus { get; set; } = AppointmentPaymentStatus.Unpaid;

    /// <summary>
    /// Sum of the line snapshots, computed by the server. The client sends only service IDs and
    /// is never trusted for price.
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Odometer reading recorded at completion; resets the recommender's interval clock.</summary>
    public int? MileageAtAppointmentKm { get; set; }

    public string? CustomerNotes { get; set; }

    /// <summary>Staff-only. Never leaves the server in a customer-facing DTO.</summary>
    public string? InternalNotes { get; set; }

    public string? CancellationReason { get; set; }
    public int? CancelledByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public int? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public ApplicationUser Customer { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public EmployeeProfile? AssignedEmployee { get; set; }

    public ICollection<AppointmentItem> Items { get; set; } = [];
    public ICollection<AppointmentStatusHistory> History { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public Review? Review { get; set; }
}
