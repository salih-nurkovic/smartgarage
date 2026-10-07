using SmartGarage.Domain.Common;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A customer's vehicle. Everything else hangs off this: appointments, reminders and the
/// recommender all start from a specific car.
/// <para>
/// This entity is the main source of recommender input features - engine type, age and mileage
/// are read directly by the scorer.
/// </para>
/// </summary>
public class Vehicle : BaseEntity, IAuditable, ISoftDeletable
{
    public int OwnerId { get; set; }
    public int BrandId { get; set; }
    public int ModelId { get; set; }
    public int EngineTypeId { get; set; }

    public int ProductionYear { get; set; }
    public string RegistrationPlate { get; set; } = string.Empty;
    public string Vin { get; set; } = string.Empty;

    public int CurrentMileageKm { get; set; }

    /// <summary>
    /// Lets the recommender tell a car that genuinely has 180 000 km from one whose odometer
    /// was last touched three years ago.
    /// </summary>
    public DateTime LastMileageUpdatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public ApplicationUser Owner { get; set; } = null!;
    public VehicleBrand Brand { get; set; } = null!;
    public VehicleModel Model { get; set; } = null!;
    public EngineType EngineType { get; set; } = null!;

    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<ServiceReminder> Reminders { get; set; } = [];
    public ICollection<UserServiceSignal> Signals { get; set; } = [];
}
