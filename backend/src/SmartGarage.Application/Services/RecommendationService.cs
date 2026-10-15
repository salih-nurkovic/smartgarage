using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Recommendations;
using SmartGarage.Application.Options;
using SmartGarage.Application.Recommender;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Enums;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IRecommendationService
{
    Task<RecommendationsResponse> GetAsync(RecommendationRequest request, CancellationToken ct);

    Task<MessageResponse> RecordClickAsync(int serviceId, RecommendationClickRequest request, CancellationToken ct);

    Task<MessageResponse> MarkDoneAsync(int serviceId, RecommendationDoneRequest request, CancellationToken ct);
}

/// <summary>
/// Gathers what the recommender needs from the database, runs <see cref="RecommenderEngine"/>
/// and records what was shown.
/// <para>
/// Every query here is a set query - one for the catalogue (cached), one for the signals, one
/// for last-done dates, one for the make-and-model statistics - so the cost of a request does
/// not grow with the number of services or signals (no query in a loop).
/// </para>
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromMinutes(10);

    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IMemoryCache _cache;
    private readonly RecommenderOptions _options;

    public RecommendationService(
        IAppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        IMemoryCache cache,
        IOptions<RecommenderOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<RecommendationsResponse> GetAsync(RecommendationRequest request, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();
        var now = _clock.UtcNow;

        var vehicle = await _db.Set<Vehicle>()
                .Where(v => v.Id == request.VehicleId && v.OwnerId == userId)
                .Select(v => new
                {
                    v.Id,
                    v.BrandId,
                    v.ModelId,
                    BrandName = v.Brand.Name,
                    ModelName = v.Model.Name,
                    v.EngineTypeId,
                    EngineCode = v.EngineType.Code,
                    EngineName = v.EngineType.Name,
                    v.ProductionYear,
                    v.CurrentMileageKm,
                })
                .SingleOrDefaultAsync(ct)
            ?? throw new ValidationException("vehicleId", "Izabrano vozilo ne postoji među vašim vozilima.");

        var context = new VehicleContext(
            vehicle.Id,
            vehicle.BrandId,
            vehicle.ModelId,
            vehicle.BrandName,
            vehicle.ModelName,
            vehicle.EngineTypeId,
            vehicle.EngineCode.ToLowerInvariant(),
            vehicle.EngineName,
            Math.Max(0, now.Year - vehicle.ProductionYear),
            vehicle.CurrentMileageKm);

        var catalog = await GetCatalogAsync(ct);

        // The customer's history with this vehicle, plus behaviour that had no vehicle attached
        // (viewing or searching the catalogue).
        var signals = await _db.Set<UserServiceSignal>()
            .Where(s => s.UserId == userId && (s.VehicleId == vehicle.Id || s.VehicleId == null))
            .Select(s => new SignalRecord(s.ServiceId, s.Weight, s.OccurredAtUtc))
            .ToListAsync(ct);

        var lastDone = await LoadLastDoneAsync(vehicle.Id, ct);
        var affinity = await LoadAffinityAsync(vehicle.BrandId, vehicle.ModelId, ct);

        var alreadyBooked = (await _db.Set<AppointmentItem>()
                .Where(i => i.Appointment.VehicleId == vehicle.Id
                            && i.Appointment.Status != AppointmentStatus.Completed
                            && i.Appointment.Status != AppointmentStatus.Cancelled)
                .Select(i => i.ServiceId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var recommendations = RecommenderEngine.Recommend(
            new RecommenderInput(context, catalog, signals, lastDone, affinity, alreadyBooked, now),
            _options);

        // Every set shown is kept with its scores and reasons, so it can be audited later and a
        // click can be traced back to the recommendation that led to it.
        foreach (var recommendation in recommendations)
        {
            _db.Set<RecommendationLog>().Add(new RecommendationLog
            {
                UserId = userId,
                VehicleId = vehicle.Id,
                ServiceId = recommendation.Service.ServiceId,
                Score = recommendation.Score,
                Rank = recommendation.Rank,
                Urgency = recommendation.Urgency,
                ExplanationText = recommendation.Explanation,
                ExplanationFactorsJson = JsonSerializer.Serialize(recommendation.Factors),
                GeneratedAtUtc = now,
                AlgorithmVersion = _options.AlgorithmVersion,
            });
        }

        await _db.SaveChangesAsync(ct);

        return new RecommendationsResponse
        {
            AlgorithmVersion = _options.AlgorithmVersion,
            Vehicle = new RecommendedVehicleResponse
            {
                VehicleId = vehicle.Id,
                Display = $"{vehicle.BrandName} {vehicle.ModelName} {vehicle.ProductionYear}",
                ProductionYear = vehicle.ProductionYear,
                MileageKm = vehicle.CurrentMileageKm,
                EngineTypeName = vehicle.EngineName,
                AgeYears = context.AgeYears,
                CompletedServiceCount = await _db.Set<Appointment>().CountAsync(
                    a => a.VehicleId == vehicle.Id && a.Status == AppointmentStatus.Completed, ct),
            },
            Items = recommendations
                .Select(r => new RecommendationItemResponse
                {
                    ServiceId = r.Service.ServiceId,
                    ServiceName = r.Service.Name,
                    CategoryName = r.Service.CategoryName,
                    Price = r.Service.Price,
                    EstimatedDurationMinutes = r.Service.DurationMinutes,
                    RecommendedMileageIntervalKm = r.Service.IntervalKm,
                    RecommendedMonthsInterval = r.Service.IntervalMonths,
                    Urgency = r.Urgency,
                    Rank = r.Rank,
                    Score = r.Score,
                    Explanation = r.Explanation,
                    Factors = r.Factors
                        .Select(f => new RecommendationFactorResponse
                        {
                            Code = f.Code,
                            Label = f.Label,
                            Contribution = Math.Round(f.Contribution, 4),
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    public async Task<MessageResponse> RecordClickAsync(
        int serviceId,
        RecommendationClickRequest request,
        CancellationToken ct)
    {
        var userId = await EnsureOwnVehicleAndServiceAsync(request.VehicleId, serviceId, ct);
        var now = _clock.UtcNow;

        _db.Set<UserServiceSignal>().Add(new UserServiceSignal
        {
            UserId = userId,
            ServiceId = serviceId,
            VehicleId = request.VehicleId,
            SignalType = SignalType.RecommendationClicked,
            Weight = SignalWeights.RecommendationClicked,
            OccurredAtUtc = now,
        });

        var shown = await _db.Set<RecommendationLog>()
            .Where(l => l.UserId == userId && l.VehicleId == request.VehicleId && l.ServiceId == serviceId)
            .OrderByDescending(l => l.GeneratedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (shown is not null)
        {
            shown.WasClicked = true;
        }

        await _db.SaveChangesAsync(ct);
        return new MessageResponse("Zabilježeno.");
    }

    public async Task<MessageResponse> MarkDoneAsync(
        int serviceId,
        RecommendationDoneRequest request,
        CancellationToken ct)
    {
        var userId = await EnsureOwnVehicleAndServiceAsync(request.VehicleId, serviceId, ct);
        var now = _clock.UtcNow;
        var performedAt = request.PerformedAtUtc ?? now;

        if (performedAt > now)
        {
            throw new ValidationException("performedAtUtc", "Datum ne može biti u budućnosti.");
        }

        var mileage = await _db.Set<Vehicle>()
            .Where(v => v.Id == request.VehicleId)
            .Select(v => v.CurrentMileageKm)
            .SingleAsync(ct);

        // Records the work and its mileage, which restarts the interval clock for this service
        // on this vehicle: the recommender counts kilometres and months from here.
        _db.Set<UserServiceSignal>().Add(new UserServiceSignal
        {
            UserId = userId,
            ServiceId = serviceId,
            VehicleId = request.VehicleId,
            SignalType = SignalType.MarkedDone,
            Weight = SignalWeights.MarkedDone,
            OccurredAtUtc = now,
            PerformedAtUtc = performedAt,
            PerformedAtMileageKm = mileage,
        });

        var openReminders = await _db.Set<ServiceReminder>()
            .Where(r => r.VehicleId == request.VehicleId
                        && r.ServiceId == serviceId
                        && (r.Status == ServiceReminderStatus.Pending || r.Status == ServiceReminderStatus.Sent))
            .ToListAsync(ct);

        foreach (var reminder in openReminders)
        {
            reminder.Status = ServiceReminderStatus.Fulfilled;
        }

        await _db.SaveChangesAsync(ct);
        return new MessageResponse("Usluga je označena kao obavljena. Preporuke su ažurirane.");
    }

    /// <summary>
    /// The catalogue as vectors only change when the catalogue does, so it is built once and
    /// cached. Catalogue and reference writes drop the entry.
    /// </summary>
    private async Task<IReadOnlyList<CatalogItem>> GetCatalogAsync(CancellationToken ct) =>
        (await _cache.GetOrCreateAsync(CacheKeys.RecommenderCatalog, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CatalogCacheDuration;

            var rows = await _db.Set<Service>()
                .AsNoTracking()
                .Where(s => s.IsActive && s.Category.IsActive)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.CategoryId,
                    CategoryName = s.Category.Name,
                    s.Price,
                    s.EstimatedDurationMinutes,
                    s.RecommendedMileageIntervalKm,
                    s.RecommendedMonthsInterval,
                    s.MinVehicleAgeYears,
                    s.MaxVehicleAgeYears,
                    EngineTypeIds = s.EngineTypeLinks.Select(l => l.EngineTypeId).ToList(),
                    Tags = s.TagLinks.Select(l => new { l.ServiceTag.Slug, l.Weight }).ToList(),
                    AverageRating = s.Reviews.Where(r => r.IsVisible).Average(r => (double?)r.Rating),
                    ReviewCount = s.Reviews.Count(r => r.IsVisible),
                })
                .OrderBy(s => s.Id)
                .ToListAsync(ct);

            return (IReadOnlyList<CatalogItem>)rows
                .Select(s => new CatalogItem(
                    s.Id,
                    s.Name,
                    s.CategoryId,
                    s.CategoryName,
                    s.Price,
                    s.EstimatedDurationMinutes,
                    s.RecommendedMileageIntervalKm,
                    s.RecommendedMonthsInterval,
                    s.MinVehicleAgeYears,
                    s.MaxVehicleAgeYears,
                    s.EngineTypeIds,
                    s.Tags.Select(t => new CatalogTag(t.Slug, t.Weight)).ToList(),
                    s.AverageRating,
                    s.ReviewCount))
                .ToList();
        }))!;

    // The latest of: a completed appointment that included the service, or the customer
    // telling us it was done elsewhere.
    private async Task<IReadOnlyDictionary<int, LastDone>> LoadLastDoneAsync(int vehicleId, CancellationToken ct)
    {
        var completed = await _db.Set<AppointmentItem>()
            .Where(i => i.Appointment.VehicleId == vehicleId
                        && i.Appointment.Status == AppointmentStatus.Completed
                        && i.Appointment.CompletedAtUtc != null)
            .Select(i => new { i.ServiceId, At = i.Appointment.CompletedAtUtc!.Value, Km = i.Appointment.MileageAtAppointmentKm })
            .ToListAsync(ct);

        var markedDone = await _db.Set<UserServiceSignal>()
            .Where(s => s.VehicleId == vehicleId && s.SignalType == SignalType.MarkedDone && s.PerformedAtUtc != null)
            .Select(s => new { s.ServiceId, At = s.PerformedAtUtc!.Value, Km = s.PerformedAtMileageKm })
            .ToListAsync(ct);

        return completed
            .Concat(markedDone)
            .GroupBy(x => x.ServiceId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.At).Select(x => new LastDone(x.At, x.Km)).First());
    }

    // How often each service has been completed on cars of this make, and of this exact model,
    // across all customers - each scaled so the most frequent service scores 1.
    private async Task<IReadOnlyDictionary<int, BrandModelAffinity>> LoadAffinityAsync(
        int brandId,
        int modelId,
        CancellationToken ct)
    {
        var counts = await _db.Set<AppointmentItem>()
            .Where(i => i.Appointment.Status == AppointmentStatus.Completed && i.Appointment.Vehicle.BrandId == brandId)
            .GroupBy(i => i.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Brand = g.Count(),
                Model = g.Count(i => i.Appointment.Vehicle.ModelId == modelId),
            })
            .ToListAsync(ct);

        var maxBrand = Math.Max(1, counts.Select(c => c.Brand).DefaultIfEmpty().Max());
        var maxModel = Math.Max(1, counts.Select(c => c.Model).DefaultIfEmpty().Max());

        return counts.ToDictionary(
            c => c.ServiceId,
            c => new BrandModelAffinity(c.Brand / (double)maxBrand, c.Model / (double)maxModel));
    }

    private async Task<int> EnsureOwnVehicleAndServiceAsync(int vehicleId, int serviceId, CancellationToken ct)
    {
        var userId = _currentUser.RequireUserId();

        var ownsVehicle = await _db.Set<Vehicle>().AnyAsync(v => v.Id == vehicleId && v.OwnerId == userId, ct);
        var serviceExists = await _db.Set<Service>().AnyAsync(s => s.Id == serviceId && s.IsActive, ct);

        return (ownsVehicle, serviceExists) switch
        {
            (false, _) => throw new ValidationException("vehicleId", "Izabrano vozilo ne postoji među vašim vozilima."),
            (_, false) => throw new NotFoundException("Usluga nije pronađena."),
            _ => userId,
        };
    }
}
