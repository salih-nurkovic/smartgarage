using SmartGarage.Domain.Common;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Domain.Entities;

/// <summary>
/// A catalogue item: what the customer books, what gets priced, and what the recommender
/// recommends.
/// </summary>
public class Service : BaseEntity, IAuditable, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }

    /// <summary>Catalogue price. Stored as decimal(18,2); money is never a floating point number.</summary>
    public decimal Price { get; set; }

    public int EstimatedDurationMinutes { get; set; }
    public string? ImagePath { get; set; }

    /// <summary>
    /// Recommended service interval in kilometres, for example 30 000 for an air filter.
    /// The mockups print this on the recommendation card ("interval: 30000 km"), and the
    /// scorer uses it to decide whether a service is merely planned or already overdue.
    /// </summary>
    public int? RecommendedMileageIntervalKm { get; set; }

    /// <summary>Recommended interval in months, for services driven by time rather than distance.</summary>
    public int? RecommendedMonthsInterval { get; set; }

    /// <summary>Lower bound of vehicle age this service suits, in years. Null means no bound.</summary>
    public int? MinVehicleAgeYears { get; set; }

    /// <summary>Upper bound of vehicle age this service suits, in years. Null means no bound.</summary>
    public int? MaxVehicleAgeYears { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public int? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }

    public ServiceCategory Category { get; set; } = null!;
    public ICollection<ServiceTagLink> TagLinks { get; set; } = [];
    public ICollection<ServiceEngineTypeLink> EngineTypeLinks { get; set; } = [];
    public ICollection<AppointmentItem> AppointmentItems { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<ServiceReminder> Reminders { get; set; } = [];
    public ICollection<UserServiceSignal> Signals { get; set; } = [];
}
