namespace SmartGarage.Domain.Entities;

/// <summary>
/// Persisted error log, queryable from the desktop administration module.
/// <para>
/// The instructions require error logging with enough information to reproduce a problem,
/// especially when the application fails during evaluation. They equally require that stack
/// traces are never returned to a client - so the trace lives here, behind an administrator
/// policy, and the client only ever receives a correlation id.
/// </para>
/// </summary>
public class ErrorLog
{
    public long Id { get; set; }

    /// <summary>
    /// Travels on the HTTP response header and on every RabbitMQ message, so a failure in the
    /// Worker can be traced back to the request that started it.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }

    /// <summary>Administrator-only. Never appears in any client-facing DTO.</summary>
    public string? StackTrace { get; set; }

    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? UserId { get; set; }
    public int? StatusCode { get; set; }

    /// <summary>"API" or "Worker".</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public ApplicationUser? User { get; set; }
}
