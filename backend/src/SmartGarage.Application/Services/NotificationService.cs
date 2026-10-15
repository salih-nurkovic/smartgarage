using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Notifications;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> GetAsync(NotificationSearchRequest search, CancellationToken ct);

    Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken ct);

    Task<NotificationResponse> MarkReadAsync(int id, CancellationToken ct);

    Task<UnreadCountResponse> MarkAllReadAsync(CancellationToken ct);

    Task<MessageResponse> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>
/// The signed-in user's notification inbox.
/// <para>
/// Every query starts from <see cref="Mine"/>, which is limited to the id in the token, so no
/// method here can read or change another user's notifications. Notifications are created by
/// the Worker, never by this service.
/// </para>
/// </summary>
public sealed class NotificationService : INotificationService
{
    // One mapping, usable both inside a SQL projection and on an entity already in memory.
    private static readonly Expression<Func<Notification, NotificationResponse>> ToResponse =
        n => new NotificationResponse
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Body = n.Body,
            IsRead = n.IsRead,
            ReadAtUtc = n.ReadAtUtc,
            CreatedAtUtc = n.CreatedAtUtc,
            RelatedEntityType = n.RelatedEntityType,
            RelatedEntityId = n.RelatedEntityId,
        };

    private static readonly Func<Notification, NotificationResponse> ToResponseInMemory = ToResponse.Compile();

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public NotificationService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private IQueryable<Notification> Mine
    {
        get
        {
            var userId = _currentUser.RequireUserId();
            return _db.Set<Notification>().Where(n => n.UserId == userId);
        }
    }

    public async Task<PagedResult<NotificationResponse>> GetAsync(
        NotificationSearchRequest search,
        CancellationToken ct)
    {
        var query = Mine.AsNoTracking();

        if (search.IsRead is { } isRead)
        {
            query = query.Where(n => n.IsRead == isRead);
        }

        if (search.Type is { } type)
        {
            query = query.Where(n => n.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip(search.Skip)
            .Take(search.ClampedPageSize)
            .Select(ToResponse)
            .ToListAsync(ct);

        return new PagedResult<NotificationResponse>(
            items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken ct) =>
        new(await Mine.CountAsync(n => !n.IsRead, ct));

    public async Task<NotificationResponse> MarkReadAsync(int id, CancellationToken ct)
    {
        var notification = await Mine.SingleOrDefaultAsync(n => n.Id == id, ct) ?? throw NotFound();

        // The time of the first read is kept. Marking an already read notification again
        // changes nothing.
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return ToResponseInMemory(notification);
    }

    public async Task<UnreadCountResponse> MarkAllReadAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // One UPDATE, however many notifications there are.
        await Mine
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAtUtc, now),
                ct);

        return new UnreadCountResponse(0);
    }

    public async Task<MessageResponse> DeleteAsync(int id, CancellationToken ct)
    {
        var notification = await Mine.SingleOrDefaultAsync(n => n.Id == id, ct) ?? throw NotFound();

        // Hidden from the inbox, not erased: the row stays as a record of what the user was told.
        notification.IsDeleted = true;
        await _db.SaveChangesAsync(ct);

        return new MessageResponse("Obavijest je obrisana.");
    }

    private static NotFoundException NotFound() => new("Obavijest nije pronađena.");
}
