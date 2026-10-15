using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Catalogue;

namespace SmartGarage.Application.Dtos.Reviews;

public sealed class ReviewResponse
{
    public int Id { get; init; }

    public int ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    /// <summary>First name and the initial of the surname, e.g. "Edin K.". The full name stays private.</summary>
    public string AuthorDisplayName { get; init; } = string.Empty;

    public int Rating { get; init; }

    public string? Comment { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public bool IsVisible { get; init; }

    /// <summary>True when the caller wrote this review and may therefore delete it.</summary>
    public bool IsMine { get; init; }
}

public sealed class ReviewCreateRequest
{
    public int AppointmentId { get; init; }

    public int ServiceId { get; init; }

    public int Rating { get; init; }

    public string? Comment { get; init; }
}

public sealed class ReviewVisibilityRequest
{
    public bool IsVisible { get; init; }
}

public sealed class ReviewSearchRequest : BaseSearchRequest
{
    public int? ServiceId { get; init; }

    public int? Rating { get; init; }

    /// <summary>Only the caller's own reviews.</summary>
    public bool Mine { get; init; }
}

public sealed class ReviewSummaryRequest
{
    public int? ServiceId { get; init; }
}

/// <summary>The average and how many reviews gave each number of stars.</summary>
public sealed class ReviewSummaryResponse
{
    public double? AverageRating { get; init; }

    public int TotalCount { get; init; }

    /// <summary>Keyed by rating, five down to one. Ratings nobody gave are present with zero.</summary>
    public IReadOnlyDictionary<int, int> Distribution { get; init; } = new Dictionary<int, int>();
}

/// <summary>A completed appointment the caller has not reviewed yet, with the services to choose from.</summary>
public sealed class ReviewableAppointmentResponse
{
    public int AppointmentId { get; init; }

    public string AppointmentNumber { get; init; } = string.Empty;

    public DateTime CompletedAtUtc { get; init; }

    public IReadOnlyList<NamedItem> Services { get; init; } = [];
}
