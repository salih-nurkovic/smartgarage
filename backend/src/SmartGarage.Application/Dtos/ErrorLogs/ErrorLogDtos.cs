using SmartGarage.Application.Common;

namespace SmartGarage.Application.Dtos.ErrorLogs;

public class ErrorLogListItemResponse
{
    public long Id { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? ExceptionType { get; init; }

    public string? HttpMethod { get; init; }

    public string? RequestPath { get; init; }

    public int? StatusCode { get; init; }

    public string? UserName { get; init; }

    public DateTime OccurredAtUtc { get; init; }
}

/// <summary>The one response in the API that carries a stack trace; administrators only.</summary>
public sealed class ErrorLogDetailResponse : ErrorLogListItemResponse
{
    public string? StackTrace { get; init; }
}

public sealed class ErrorLogSearchRequest : BaseSearchRequest
{
    /// <summary>"API" or "Worker".</summary>
    public string? Source { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}
