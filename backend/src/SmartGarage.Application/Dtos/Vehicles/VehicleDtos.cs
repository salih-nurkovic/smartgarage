using SmartGarage.Application.Common;

namespace SmartGarage.Application.Dtos.Vehicles;

public sealed class VehicleResponse
{
    public int Id { get; init; }

    public string OwnerFullName { get; init; } = string.Empty;

    public int BrandId { get; init; }

    public string BrandName { get; init; } = string.Empty;

    public int ModelId { get; init; }

    public string ModelName { get; init; } = string.Empty;

    public int EngineTypeId { get; init; }

    public string EngineTypeName { get; init; } = string.Empty;

    public int ProductionYear { get; init; }

    public string RegistrationPlate { get; init; } = string.Empty;

    public string Vin { get; init; } = string.Empty;

    public int CurrentMileageKm { get; init; }

    public DateTime LastMileageUpdatedAtUtc { get; init; }

    /// <summary>When the last completed appointment for this vehicle finished.</summary>
    public DateTime? LastServiceAtUtc { get; init; }

    /// <summary>The earliest pending reminder, shown as "Sljedeći servis" on the vehicle card.</summary>
    public DateTime? NextServiceDueAtUtc { get; init; }

    public int AppointmentCount { get; init; }
}

/// <summary>There is no owner here: a vehicle is always created for the signed-in customer.</summary>
public sealed class VehicleRequest
{
    public int BrandId { get; init; }

    public int ModelId { get; init; }

    public int EngineTypeId { get; init; }

    public int ProductionYear { get; init; }

    public string RegistrationPlate { get; init; } = string.Empty;

    public string Vin { get; init; } = string.Empty;

    public int CurrentMileageKm { get; init; }
}

public sealed class VehicleMileageRequest
{
    public int CurrentMileageKm { get; init; }
}

public sealed class VehicleSearchRequest : BaseSearchRequest
{
    /// <summary>Honoured for staff only. A customer always gets their own vehicles.</summary>
    public int? OwnerId { get; init; }

    public int? BrandId { get; init; }

    public int? EngineTypeId { get; init; }
}
