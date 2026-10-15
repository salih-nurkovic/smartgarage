using SmartGarage.Application.Common;

namespace SmartGarage.Application.Dtos.Catalogue;

/// <summary>An id with the name a person recognises it by. Ids alone are never shown.</summary>
public sealed record NamedItem(int Id, string Name);

public sealed class ServiceResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int EstimatedDurationMinutes { get; init; }

    /// <summary>Where to fetch the image, or null when the service has none.</summary>
    public string? ImageUrl { get; init; }

    public int? RecommendedMileageIntervalKm { get; init; }

    public int? RecommendedMonthsInterval { get; init; }

    public int? MinVehicleAgeYears { get; init; }

    public int? MaxVehicleAgeYears { get; init; }

    public bool IsActive { get; init; }

    public double? AverageRating { get; init; }

    public int ReviewCount { get; init; }

    public int BookingCount { get; init; }

    public IReadOnlyList<NamedItem> Tags { get; init; } = [];

    public IReadOnlyList<NamedItem> EngineTypes { get; init; } = [];
}

public sealed class ServiceRequest
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public decimal Price { get; init; }

    public int EstimatedDurationMinutes { get; init; }

    public int? RecommendedMileageIntervalKm { get; init; }

    public int? RecommendedMonthsInterval { get; init; }

    public int? MinVehicleAgeYears { get; init; }

    public int? MaxVehicleAgeYears { get; init; }

    public bool IsActive { get; init; } = true;

    public IReadOnlyList<int> TagIds { get; init; } = [];

    /// <summary>Empty means the service applies to every engine type.</summary>
    public IReadOnlyList<int> EngineTypeIds { get; init; } = [];
}

public sealed class ServiceSearchRequest : BaseSearchRequest
{
    public int? CategoryId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public int? MaxDurationMinutes { get; init; }

    public int? EngineTypeId { get; init; }

    // A concrete List: the query-string binder fills a list it can add to, and silently
    // leaves a read-only collection empty.
#pragma warning disable CA1002
    public List<int> TagIds { get; init; } = [];
#pragma warning restore CA1002

    public bool? IsActive { get; init; }
}
