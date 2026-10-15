using SmartGarage.Domain.Enums;

namespace SmartGarage.Application.Recommender;

/// <summary>The vehicle recommendations are made for, with what is known about it.</summary>
public sealed record VehicleContext(
    int VehicleId,
    int BrandId,
    int ModelId,
    string BrandName,
    string ModelName,
    int EngineTypeId,
    string EngineCode,
    string EngineName,
    int AgeYears,
    int MileageKm);

public sealed record CatalogTag(string Slug, double Weight);

/// <summary>A bookable service and the content features it is described by.</summary>
public sealed record CatalogItem(
    int ServiceId,
    string Name,
    int CategoryId,
    string CategoryName,
    decimal Price,
    int DurationMinutes,
    int? IntervalKm,
    int? IntervalMonths,
    int? MinAgeYears,
    int? MaxAgeYears,
    IReadOnlyList<int> EngineTypeIds,
    IReadOnlyList<CatalogTag> Tags,
    double? AverageRating,
    int ReviewCount);

/// <summary>One stored behavioural signal: who did what with which service, how strongly, when.</summary>
public sealed record SignalRecord(int ServiceId, double Weight, DateTime OccurredAtUtc);

/// <summary>When a service was last done on this vehicle, and at what mileage if known.</summary>
public sealed record LastDone(DateTime AtUtc, int? MileageKm);

/// <summary>
/// How often a service is done on vehicles like this one, relative to the most common service:
/// 1 for the most frequent, 0 for one never done.
/// </summary>
public sealed record BrandModelAffinity(double BrandShare, double ModelShare);

public sealed record RecommenderInput(
    VehicleContext Vehicle,
    IReadOnlyList<CatalogItem> Catalog,
    IReadOnlyList<SignalRecord> Signals,
    IReadOnlyDictionary<int, LastDone> LastDone,
    IReadOnlyDictionary<int, BrandModelAffinity> Affinity,
    IReadOnlySet<int> ServicesAlreadyBooked,
    DateTime NowUtc);

/// <summary>One named part of a score. Negative ones lower the score and are never used as reasons.</summary>
public sealed record ScoreFactor(string Code, string Label, double Contribution);

public sealed record Recommendation(
    CatalogItem Service,
    double Score,
    int Rank,
    RecommendationUrgency Urgency,
    string Explanation,
    IReadOnlyList<ScoreFactor> Factors);

public static class FactorCodes
{
    public const string Similarity = "SIMILARITY";
    public const string HistoryCategory = "HISTORY_CATEGORY";
    public const string MileageDue = "MILEAGE_DUE";
    public const string TimeDue = "TIME_DUE";
    public const string EngineMatch = "ENGINE_MATCH";
    public const string EngineAny = "ENGINE_ANY";
    public const string AgeFit = "AGE_FIT";
    public const string Rating = "RATING";
    public const string BrandModel = "BRAND_MODEL";
    public const string RecentlyDone = "RECENTLY_DONE";
}
