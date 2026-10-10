namespace SmartGarage.Application.Common;

/// <summary>
/// The one shape every error response has. It never carries a stack trace or an internal
/// message: the correlation id is what links a client report to the full entry in the error log.
/// </summary>
public sealed class ApiErrorResponse
{
    public required int Status { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }

    public required string CorrelationId { get; init; }

    /// <summary>Validation messages keyed by camelCase field name; null for other errors.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
