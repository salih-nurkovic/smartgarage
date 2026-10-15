namespace SmartGarage.Application.Options;

/// <summary>
/// Every tunable number of the recommender, from <c>.env</c>. Changing a weight changes the
/// ranking without a rebuild; the documentation lists what each one does.
/// </summary>
public sealed class RecommenderOptions
{
    public int TopN { get; set; }

    public string AlgorithmVersion { get; set; } = string.Empty;

    /// <summary>After this many days a signal counts half as much as on the day it happened.</summary>
    public double SignalHalfLifeDays { get; set; }

    /// <summary>With this many signals or more, history has its full say in the profile.</summary>
    public int SignalSaturationCount { get; set; }

    /// <summary>Once history has its full say, how much of the profile it makes up.</summary>
    public double HistoryBlendBeta { get; set; }

    public int HighMileageThresholdKm { get; set; }

    public int OldVehicleAgeYears { get; set; }

    public double EngineWeight { get; set; }

    public double DueWeight { get; set; }

    public double AgeWeight { get; set; }

    public double RatingWeight { get; set; }

    public double BrandModelWeight { get; set; }

    public double RecentPenalty { get; set; }

    public double ExplanationMinContribution { get; set; }

    /// <summary>Share of the recommended interval at which a service becomes "Hitno".</summary>
    public double UrgentThreshold { get; set; }
}
