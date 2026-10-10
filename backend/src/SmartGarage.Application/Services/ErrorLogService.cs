using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Application.Services;

public sealed record ErrorLogEntry(
    string CorrelationId,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? RequestPath,
    string? HttpMethod,
    int? UserId,
    int StatusCode);

/// <summary>
/// Persists unexpected failures so an administrator can read them later. This is where a stack
/// trace goes instead of into the response.
/// </summary>
public interface IErrorLogService
{
    Task RecordAsync(ErrorLogEntry entry, CancellationToken ct);
}

public sealed class ErrorLogService : IErrorLogService
{
    private const string ApiSource = "API";
    private const string ErrorLevel = "Error";
    private const int MessageMaxLength = 2000;
    private const int ExceptionTypeMaxLength = 300;
    private const int RequestPathMaxLength = 500;

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ErrorLogService(IAppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task RecordAsync(ErrorLogEntry entry, CancellationToken ct)
    {
        _db.ErrorLogs.Add(new ErrorLog
        {
            CorrelationId = entry.CorrelationId,
            Level = ErrorLevel,
            Message = Truncate(entry.Message, MessageMaxLength)!,
            ExceptionType = Truncate(entry.ExceptionType, ExceptionTypeMaxLength),
            StackTrace = entry.StackTrace,
            RequestPath = Truncate(entry.RequestPath, RequestPathMaxLength),
            HttpMethod = entry.HttpMethod,
            UserId = entry.UserId,
            StatusCode = entry.StatusCode,
            Source = ApiSource,
            OccurredAtUtc = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
