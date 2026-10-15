using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Dtos.Recommendations;

public sealed class RecommendationRequest
{
    public int VehicleId { get; init; }
}

/// <summary>The header of the recommendations screen: which vehicle, and what is known about it.</summary>
public sealed class RecommendedVehicleResponse
{
    public int VehicleId { get; init; }

    public string Display { get; init; } = string.Empty;

    public int ProductionYear { get; init; }

    public int MileageKm { get; init; }

    public string EngineTypeName { get; init; } = string.Empty;

    public int AgeYears { get; init; }

    public int CompletedServiceCount { get; init; }
}

public sealed class RecommendationFactorResponse
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public double Contribution { get; init; }
}

public sealed class RecommendationItemResponse
{
    public int ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int EstimatedDurationMinutes { get; init; }

    public int? RecommendedMileageIntervalKm { get; init; }

    public int? RecommendedMonthsInterval { get; init; }

    public RecommendationUrgency Urgency { get; init; }

    public int Rank { get; init; }

    public double Score { get; init; }

    /// <summary>Why this service is recommended, in a sentence the customer reads on the card.</summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>Every named part of the score, for a "why?" panel and for checking the maths.</summary>
    public IReadOnlyList<RecommendationFactorResponse> Factors { get; init; } = [];
}

public sealed class RecommendationsResponse
{
    public RecommendedVehicleResponse Vehicle { get; init; } = new();

    /// <summary>Shown as a badge on the screen, as in the approved mockup.</summary>
    public string Algorithm { get; init; } = "Content-Based";

    public string AlgorithmVersion { get; init; } = string.Empty;

    public IReadOnlyList<RecommendationItemResponse> Items { get; init; } = [];
}

/// <summary>"Zakaži" on a recommendation card.</summary>
public sealed class RecommendationClickRequest
{
    public int VehicleId { get; init; }
}

/// <summary>"Obavljeno": the customer already had this service done somewhere else.</summary>
public sealed class RecommendationDoneRequest
{
    public int VehicleId { get; init; }

    /// <summary>When it was done. Defaults to now.</summary>
    public DateTime? PerformedAtUtc { get; init; }
}
