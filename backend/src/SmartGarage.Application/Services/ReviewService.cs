using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Application.Dtos.Reviews;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IReviewService
{
    Task<PagedResult<ReviewResponse>> GetAsync(ReviewSearchRequest search, CancellationToken ct);

    Task<ReviewSummaryResponse> GetSummaryAsync(ReviewSummaryRequest request, CancellationToken ct);

    Task<IReadOnlyList<ReviewableAppointmentResponse>> GetReviewableAsync(CancellationToken ct);

    Task<ReviewResponse> CreateAsync(ReviewCreateRequest request, CancellationToken ct);

    Task<ReviewResponse> SetVisibilityAsync(int id, ReviewVisibilityRequest request, CancellationToken ct);

    Task<MessageResponse> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>
/// Reviews of completed work.
/// <para>
/// A review can only be written by the customer the appointment belongs to, only after the
/// appointment is completed, and only once. The instructions list "reviewing before
/// completion" among the things the server must refuse, so none of this is left to a client.
/// </para>
/// </summary>
public sealed class ReviewService : IReviewService
{
    private const int LowestPositiveRating = 4;
    private const int HighestNegativeRating = 2;

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ReviewService(IAppDbContext db, ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    private bool IsAdministrator => _currentUser.IsInRole(Roles.Administrator);

    // A hidden review stays visible to the administrator who moderates and to its author;
    // nobody else sees it.
    private IQueryable<Review> Visible
    {
        get
        {
            if (IsAdministrator)
            {
                return _db.Set<Review>();
            }

            var userId = _currentUser.RequireUserId();
            return _db.Set<Review>().Where(r => r.IsVisible || r.AuthorId == userId);
        }
    }

    public async Task<PagedResult<ReviewResponse>> GetAsync(ReviewSearchRequest search, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var query = Visible.AsNoTracking();

        if (search.ServiceId is { } serviceId)
        {
            query = query.Where(r => r.ServiceId == serviceId);
        }

        if (search.Rating is { } rating)
        {
            query = query.Where(r => r.Rating == rating);
        }

        if (search.Mine)
        {
            query = query.Where(r => r.AuthorId == userId);
        }

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(r => r.Comment!.Contains(term) || r.Service.Name.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await Project(
                query.OrderByDescending(r => r.CreatedAtUtc)
                    .ThenByDescending(r => r.Id)
                    .Skip(search.Skip)
                    .Take(search.ClampedPageSize),
                userId)
            .ToListAsync(ct);

        return new PagedResult<ReviewResponse>(items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    // One GROUP BY. The average is derived from the same five numbers, so the figure and the
    // bars under it can never disagree.
    public async Task<ReviewSummaryResponse> GetSummaryAsync(ReviewSummaryRequest request, CancellationToken ct)
    {
        var query = _db.Set<Review>().Where(r => r.IsVisible);

        if (request.ServiceId is { } serviceId)
        {
            query = query.Where(r => r.ServiceId == serviceId);
        }

        var counts = await query
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Rating, g => g.Count, ct);

        var total = counts.Values.Sum();

        return new ReviewSummaryResponse
        {
            TotalCount = total,
            AverageRating = total == 0
                ? null
                : Math.Round(counts.Sum(c => (double)c.Key * c.Value) / total, 1),
            Distribution = Enumerable
                .Range(ValidationRules.RatingMin, ValidationRules.RatingMax - ValidationRules.RatingMin + 1)
                .Reverse()
                .ToDictionary(rating => rating, rating => counts.GetValueOrDefault(rating)),
        };
    }

    public async Task<IReadOnlyList<ReviewableAppointmentResponse>> GetReviewableAsync(CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        return await _db.Set<Appointment>()
            .AsNoTracking()
            .Where(a => a.CustomerId == userId
                        && a.Status == AppointmentStatus.Completed
                        && a.Review == null)
            .OrderByDescending(a => a.CompletedAtUtc)
            .Take(PagingLimits.MaxPageSize)
            .Select(a => new ReviewableAppointmentResponse
            {
                AppointmentId = a.Id,
                AppointmentNumber = a.AppointmentNumber,
                CompletedAtUtc = a.CompletedAtUtc!.Value,
                Services = a.Items.Select(i => new NamedItem(i.ServiceId, i.Service.Name)).ToList(),
            })
            .ToListAsync(ct);
    }

    public async Task<ReviewResponse> CreateAsync(ReviewCreateRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var appointment = await _db.Set<Appointment>()
                .Where(a => a.Id == request.AppointmentId && a.CustomerId == userId)
                .Select(a => new
                {
                    a.Status,
                    a.VehicleId,
                    a.AssignedEmployeeId,
                    HasReview = a.Review != null,
                    ServiceIds = a.Items.Select(i => i.ServiceId).ToList(),
                })
                .SingleOrDefaultAsync(ct)
            ?? throw new ValidationException("appointmentId", "Izabrana rezervacija ne postoji.");

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new BusinessRuleException("Recenziju možete ostaviti tek nakon što servis bude završen.");
        }

        if (appointment.HasReview)
        {
            throw new ConflictException("Za ovu rezervaciju ste već ostavili recenziju.");
        }

        if (!appointment.ServiceIds.Contains(request.ServiceId))
        {
            throw new ValidationException("serviceId", "Izabrana usluga nije dio ove rezervacije.");
        }

        var now = _clock.UtcNow;

        var review = new Review
        {
            AppointmentId = request.AppointmentId,
            ServiceId = request.ServiceId,
            AuthorId = userId,

            // Copied from the appointment so a mechanic's average is one indexed aggregate.
            AssignedEmployeeId = appointment.AssignedEmployeeId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAtUtc = now,
            IsVisible = true,
        };

        _db.Set<Review>().Add(review);

        // A rating in the middle says nothing either way, so it leaves no signal.
        if (request.Rating is >= LowestPositiveRating or <= HighestNegativeRating)
        {
            _db.Set<UserServiceSignal>().Add(new UserServiceSignal
            {
                UserId = userId,
                ServiceId = request.ServiceId,
                VehicleId = appointment.VehicleId,
                SignalType = SignalType.Reviewed,
                Weight = request.Rating >= LowestPositiveRating
                    ? SignalWeights.ReviewedPositive
                    : SignalWeights.ReviewedNegative,
                OccurredAtUtc = now,
            });
        }

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(review.Id, userId, ct);
    }

    public async Task<ReviewResponse> SetVisibilityAsync(int id, ReviewVisibilityRequest request, CancellationToken ct)
    {
        var review = await _db.Set<Review>().SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw NotFound();

        review.IsVisible = request.IsVisible;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, _currentUser.RequireUserId(), ct);
    }

    public async Task<MessageResponse> DeleteAsync(int id, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        // An administrator may remove any review; everyone else only their own. Somebody
        // else's review is reported as not found rather than forbidden.
        var review = await _db.Set<Review>()
                .SingleOrDefaultAsync(r => r.Id == id && (IsAdministrator || r.AuthorId == userId), ct)
            ?? throw NotFound();

        _db.Set<Review>().Remove(review);
        await _db.SaveChangesAsync(ct);

        return new MessageResponse("Recenzija je obrisana.");
    }

    private async Task<ReviewResponse> GetByIdAsync(int id, int userId, CancellationToken ct) =>
        await Project(_db.Set<Review>().AsNoTracking().Where(r => r.Id == id), userId).SingleAsync(ct);

    private static IQueryable<ReviewResponse> Project(IQueryable<Review> query, int userId) =>
        query.Select(r => new ReviewResponse
        {
            Id = r.Id,
            ServiceId = r.ServiceId,
            ServiceName = r.Service.Name,

            // Translated to SQL (SUBSTRING), where a span has no meaning.
#pragma warning disable CA1845
            AuthorDisplayName = r.Author.FirstName + " " + r.Author.LastName.Substring(0, 1) + ".",
#pragma warning restore CA1845

            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAtUtc = r.CreatedAtUtc,
            IsVisible = r.IsVisible,
            IsMine = r.AuthorId == userId,
        });

    private static NotFoundException NotFound() => new("Recenzija nije pronađena.");
}
