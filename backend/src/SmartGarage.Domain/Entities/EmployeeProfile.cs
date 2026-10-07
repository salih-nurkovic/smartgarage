using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// Employment data for a user who works in the garage, kept out of <see cref="ApplicationUser"/>
/// so the person table stays about people and this one about employment.
/// <para>
/// An employee is an ordinary Identity user carrying the Employee role, plus this 1:1 record.
/// There are never two records for the same person.
/// </para>
/// </summary>
public class EmployeeProfile : BaseEntity, IAuditable
{
    public int UserId { get; set; }
    public string Position { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public DateTime HireDateUtc { get; set; }

    /// <summary>When false the employee is skipped by the slot availability calculation.</summary>
    public bool IsAvailableForAssignment { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public ICollection<EmployeeSchedule> Schedules { get; set; } = [];
    public ICollection<Appointment> AssignedAppointments { get; set; } = [];
}
