namespace SmartGarage.Domain.Constants;

/// <summary>
/// Paging bounds. Every list endpoint is paged and every page size is clamped server-side,
/// because the instructions treat unbounded list endpoints as a defect and a DoS surface.
/// </summary>
public static class PagingLimits
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinPageSize = 1;

    /// <summary>Forces any client-supplied page size into the allowed range.</summary>
    public static int ClampPageSize(int requested) =>
        requested < MinPageSize ? DefaultPageSize :
        requested > MaxPageSize ? MaxPageSize :
        requested;

    /// <summary>Forces any client-supplied page number to be at least the first page.</summary>
    public static int ClampPage(int requested) => requested < DefaultPage ? DefaultPage : requested;
}
