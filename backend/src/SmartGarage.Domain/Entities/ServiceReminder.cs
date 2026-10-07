using SmartGarage.Domain.Common;
using SmartGarage.Domain.Enums;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A maintenance reminder for one service on one vehicle, generated when an appointment
/// completes.
/// <para>
/// Due by date, by mileage, or both. The Worker scans these on a schedule, which is genuine
/// asynchronous work rather than something invented to justify a second service. The approved
/// registration promises "automatske podsjetnike na osnovu servisne historije vozila".
/// </para>
/// </summary>
public class ServiceReminder : BaseEntity
{
    public int VehicleId { get; set; }
    public int ServiceId { get; set; }

    public DateTime? DueDateUtc { get; set; }
    public int? DueMileageKm { get; set; }

    public ServiceReminderStatus Status { get; set; } = ServiceReminderStatus.Pending;

    /// <summary>The completed appointment that produced this reminder, when there was one.</summary>
    public int? GeneratedFromAppointmentId { get; set; }

    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
    public Service Service { get; set; } = null!;
    public Appointment? GeneratedFromAppointment { get; set; }
}
