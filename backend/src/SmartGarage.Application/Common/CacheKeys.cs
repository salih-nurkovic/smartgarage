namespace SmartGarage.Application.Common;

/// <summary>Cache keys, declared once so the code that fills an entry and the code that drops it agree.</summary>
public static class CacheKeys
{
    /// <summary>The bundle of active reference lists that fills every dropdown.</summary>
    public const string Lookup = "lookup:bundle";
}
