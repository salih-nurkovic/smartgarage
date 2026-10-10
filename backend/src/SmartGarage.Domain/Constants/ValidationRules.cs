using System.Text.RegularExpressions;

namespace SmartGarage.Domain.Constants;

/// <summary>
/// Validation bounds shared by the server-side validators and (mirrored) by both Flutter
/// clients, so the same numbers are never written twice with different values. The
/// instructions forbid magic numbers; these are the single source for them.
/// </summary>
public static partial class ValidationRules
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 50;
    public const int EmailMaxLength = 256;

    public const int BrandNameMaxLength = 60;
    public const int ModelNameMaxLength = 80;
    public const int EngineTypeNameMaxLength = 40;
    public const int CategoryNameMaxLength = 80;
    public const int CategoryDescriptionMaxLength = 500;
    public const int DisplayOrderMax = 1000;
    public const int TagNameMaxLength = 60;
    public const int ServiceNameMaxLength = 120;
    public const int ServiceDescriptionMaxLength = 1000;

    public const int MileageIntervalMinKm = 1000;
    public const int MileageIntervalMaxKm = 500_000;
    public const int MonthsIntervalMax = 240;
    public const int VehicleAgeMaxYears = 100;

    public const int VinLength = 17;
    public const int PlateMinLength = 3;
    public const int PlateMaxLength = 15;
    public const int VehicleYearMin = 1950;
    public const int MileageMaxKm = 3_000_000;

    public const decimal PriceMin = 0.01m;
    public const decimal PriceMax = 100_000m;
    public const int DurationMinMinutes = 5;
    public const int DurationMaxMinutes = 1440;
    public const int DurationStepMinutes = 5;

    public const int RatingMin = 1;
    public const int RatingMax = 5;

    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;

    /// <summary>VIN excludes I, O and Q by the ISO standard.</summary>
    [GeneratedRegex(@"^[A-HJ-NPR-Z0-9]{17}$")]
    public static partial Regex Vin();

    [GeneratedRegex(@"^\+?[0-9\s\-/()]{6,20}$")]
    public static partial Regex Phone();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    public static partial Regex Email();

    [GeneratedRegex(@"^[A-Za-z0-9_]{2,20}$")]
    public static partial Regex EngineTypeCode();

    /// <summary>Lowercase words joined by single hyphens, the form the recommender matches tags by.</summary>
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex Slug();

    /// <summary>Usernames are what people log in with, so they stay simple and predictable.</summary>
    [GeneratedRegex(@"^[a-zA-Z0-9._-]{3,50}$")]
    public static partial Regex UserName();
}
