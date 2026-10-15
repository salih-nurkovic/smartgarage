namespace SmartGarage.Domain.Constants;

/// <summary>
/// How strongly each kind of behaviour counts towards (or against) a service in the
/// recommender. Written into the signal row when it is recorded, and read back by the scorer.
/// </summary>
public static class SignalWeights
{
    public const double Searched = 0.3;
    public const double Viewed = 0.5;
    public const double RecommendationClicked = 1.0;
    public const double MarkedDone = 1.5;
    public const double Booked = 2.0;
    public const double Completed = 3.0;
    public const double Cancelled = -1.0;
    public const double ReviewedPositive = 2.5;
    public const double ReviewedNegative = -1.5;
}
