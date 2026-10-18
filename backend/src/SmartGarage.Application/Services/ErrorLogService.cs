using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.ErrorLogs;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

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

    Task<PagedResult<ErrorLogListItemResponse>> GetAsync(ErrorLogSearchRequest search, CancellationToken ct);

    Task<ErrorLogDetailResponse> GetByIdAsync(long id, CancellationToken ct);
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

    public async Task<PagedResult<ErrorLogListItemResponse>> GetAsync(ErrorLogSearchRequest search, CancellationToken ct)
    {
        var query = _db.ErrorLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search.Source))
        {
            query = query.Where(e => e.Source == search.Source);
        }

        if (search.From is { } from)
        {
            query = query.Where(e => e.OccurredAtUtc >= from);
        }

        if (search.To is { } to)
        {
            query = query.Where(e => e.OccurredAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();

            // Translated to SQL LIKE; how text compares is the column collation's business.
#pragma warning disable CA1307
            query = query.Where(e =>
                e.CorrelationId.Contains(term)
                || e.Message.Contains(term)
                || (e.ExceptionType != null && e.ExceptionType.Contains(term))
                || (e.RequestPath != null && e.RequestPath.Contains(term)));
#pragma warning restore CA1307
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip(search.Skip)
            .Take(search.ClampedPageSize)
            .Select(e => new ErrorLogListItemResponse
            {
                Id = e.Id,
                CorrelationId = e.CorrelationId,
                Source = e.Source,
                Level = e.Level,
                Message = e.Message,
                ExceptionType = e.ExceptionType,
                HttpMethod = e.HttpMethod,
                RequestPath = e.RequestPath,
                StatusCode = e.StatusCode,
                UserName = e.User == null ? null : e.User.UserName,
                OccurredAtUtc = e.OccurredAtUtc,
            })
            .ToListAsync(ct);

        return new PagedResult<ErrorLogListItemResponse>(items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<ErrorLogDetailResponse> GetByIdAsync(long id, CancellationToken ct) =>
        await _db.ErrorLogs
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new ErrorLogDetailResponse
            {
                Id = e.Id,
                CorrelationId = e.CorrelationId,
                Source = e.Source,
                Level = e.Level,
                Message = e.Message,
                ExceptionType = e.ExceptionType,
                HttpMethod = e.HttpMethod,
                RequestPath = e.RequestPath,
                StatusCode = e.StatusCode,
                UserName = e.User == null ? null : e.User.UserName,
                OccurredAtUtc = e.OccurredAtUtc,
                StackTrace = e.StackTrace,
            })
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Zapis greške nije pronađen.");

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
