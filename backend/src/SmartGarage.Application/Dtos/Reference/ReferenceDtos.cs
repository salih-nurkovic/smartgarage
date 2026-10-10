using SmartGarage.Application.Common;

namespace SmartGarage.Application.Dtos.Reference;

public class ReferenceSearchRequest : BaseSearchRequest
{
    public bool? IsActive { get; init; }
}

public sealed class VehicleModelSearchRequest : ReferenceSearchRequest
{
    public int? BrandId { get; init; }
}

// --- Vehicle brands ---------------------------------------------------------

public sealed class VehicleBrandResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int ModelCount { get; init; }
}

public sealed class VehicleBrandRequest
{
    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

// --- Vehicle models ---------------------------------------------------------

public sealed class VehicleModelResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int BrandId { get; init; }

    public string BrandName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int VehicleCount { get; init; }
}

public sealed class VehicleModelRequest
{
    public string Name { get; init; } = string.Empty;

    public int BrandId { get; init; }

    public bool IsActive { get; init; } = true;
}

// --- Engine types -----------------------------------------------------------

public sealed class EngineTypeResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class EngineTypeRequest
{
    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}

// --- Service categories -----------------------------------------------------

public sealed class ServiceCategoryResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsActive { get; init; }

    public int ServiceCount { get; init; }
}

public sealed class ServiceCategoryRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsActive { get; init; } = true;
}

// --- Service tags -----------------------------------------------------------

public sealed class ServiceTagResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int ServiceCount { get; init; }
}

public sealed class ServiceTagRequest
{
    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}
