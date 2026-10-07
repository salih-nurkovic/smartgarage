using SmartGarage.Domain.Common;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// One service booked on an appointment, with price and duration captured at booking time.
/// <para>
/// The snapshots are the point of this entity. If the catalogue price of an oil change changes
/// next month, last month's appointment total, its payment and every revenue report that
/// includes it must stay exactly as they were. Reading the price through a join to
/// <see cref="Service"/> would silently rewrite history.
/// </para>
/// </summary>
public class AppointmentItem : BaseEntity
{
    public int AppointmentId { get; set; }
    public int ServiceId { get; set; }

    /// <summary>Catalogue price at the moment of booking.</summary>
    public decimal UnitPriceSnapshot { get; set; }

    /// <summary>Estimated duration at the moment of booking.</summary>
    public int DurationMinutesSnapshot { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>UnitPriceSnapshot multiplied by Quantity, computed in the service layer.</summary>
    public decimal LineTotal { get; set; }

    public Appointment Appointment { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
