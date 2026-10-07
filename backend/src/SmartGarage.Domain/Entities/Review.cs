using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A rating and comment left after a completed appointment.
/// <para>
/// Eligibility is enforced on the server: the appointment must be Completed, must belong to the
/// author, and must not already have a review. The instructions name reviewing before
/// completion as a business rule that has to be blocked.
/// </para>
/// </summary>
public class Review : BaseEntity
{
    /// <summary>Unique: one review per appointment.</summary>
    public int AppointmentId { get; set; }

    /// <summary>The service being reviewed, chosen from a dropdown on the review form.</summary>
    public int ServiceId { get; set; }

    public int AuthorId { get; set; }

    /// <summary>
    /// Denormalised so the desktop employee cards can show an average rating per mechanic
    /// without walking back through the appointment on every row.
    /// </summary>
    public int? AssignedEmployeeId { get; set; }

    /// <summary>1 to 5, enforced by a check constraint in the database.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Administrators can hide a review without destroying it.</summary>
    public bool IsVisible { get; set; } = true;

    public Appointment Appointment { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
    public EmployeeProfile? AssignedEmployee { get; set; }
}
