using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Domain.Constants;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Entities.Reference;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface IServiceCatalogService : ICrudService<ServiceResponse, ServiceSearchRequest, ServiceRequest>
{
    Task<FileDownload> GetImageAsync(int id, CancellationToken ct);

    Task<ServiceResponse> SetImageAsync(int id, FileUpload file, CancellationToken ct);
}

/// <summary>The service catalogue: what the garage offers, at what price, for which engines.</summary>
public sealed class ServiceCatalogService
    : CrudService<Service, ServiceResponse, ServiceSearchRequest, ServiceRequest>, IServiceCatalogService
{
    private const string ImageFolder = "services";

    private readonly ICurrentUserService _currentUser;
    private readonly IImageStore _images;

    public ServiceCatalogService(IAppDbContext db, ICurrentUserService currentUser, IImageStore images)
        : base(db)
    {
        _currentUser = currentUser;
        _images = images;
    }

    protected override string NotFoundMessage => "Usluga nije pronađena.";

    protected override string DeletedMessage => "Usluga je obrisana iz kataloga.";

    private bool IsStaff =>
        _currentUser.IsInRole(Roles.Administrator) || _currentUser.IsInRole(Roles.Employee);

    // Customers only ever see what can actually be booked. Staff see inactive services too,
    // because they are the ones who switch them back on.
    protected override IQueryable<Service> Query =>
        IsStaff ? Db.Set<Service>() : Db.Set<Service>().Where(s => s.IsActive && s.Category.IsActive);

    public async Task<FileDownload> GetImageAsync(int id, CancellationToken ct)
    {
        var path = await Query.Where(s => s.Id == id).Select(s => s.ImagePath).SingleOrDefaultAsync(ct);

        return _images.Open(path) ?? throw new NotFoundException("Usluga nema sliku.");
    }

    public async Task<ServiceResponse> SetImageAsync(int id, FileUpload file, CancellationToken ct)
    {
        var service = await LoadAsync(id, ct);
        var previous = service.ImagePath;

        service.ImagePath = await _images.SaveAsync(ImageFolder, file, ct);
        await Db.SaveChangesAsync(ct);

        // Only after the new path is committed, so a failed save never leaves the row
        // pointing at a file that is gone.
        _images.Delete(previous);

        return await GetByIdAsync(id, ct);
    }

    // Every filter here becomes part of the SQL WHERE clause. Nothing is loaded and then
    // filtered in memory, which the instructions name as a defect.
    protected override IQueryable<Service> Filter(IQueryable<Service> query, ServiceSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.Description.Contains(term));
        }

        if (search.CategoryId is { } categoryId)
        {
            query = query.Where(s => s.CategoryId == categoryId);
        }

        if (search.MinPrice is { } minPrice)
        {
            query = query.Where(s => s.Price >= minPrice);
        }

        if (search.MaxPrice is { } maxPrice)
        {
            query = query.Where(s => s.Price <= maxPrice);
        }

        if (search.MaxDurationMinutes is { } maxDuration)
        {
            query = query.Where(s => s.EstimatedDurationMinutes <= maxDuration);
        }

        // A service with no engine types listed applies to all of them.
        if (search.EngineTypeId is { } engineTypeId)
        {
            query = query.Where(s =>
                s.EngineTypeLinks.Count == 0
                || s.EngineTypeLinks.Any(l => l.EngineTypeId == engineTypeId));
        }

        if (search.TagIds.Count > 0)
        {
            query = query.Where(s => s.TagLinks.Any(l => search.TagIds.Contains(l.ServiceTagId)));
        }

        return search.IsActive is { } isActive ? query.Where(s => s.IsActive == isActive) : query;
    }

    protected override IOrderedQueryable<Service> Sort(IQueryable<Service> query, ServiceSearchRequest search) =>
        search.OrderBy?.ToUpperInvariant() switch
        {
            "PRICE" => search.Descending
                ? query.OrderByDescending(s => s.Price).ThenBy(s => s.Name)
                : query.OrderBy(s => s.Price).ThenBy(s => s.Name),
            "DURATION" => search.Descending
                ? query.OrderByDescending(s => s.EstimatedDurationMinutes).ThenBy(s => s.Name)
                : query.OrderBy(s => s.EstimatedDurationMinutes).ThenBy(s => s.Name),
            _ => SortByNameOrNewest(query, search, s => s.Name),
        };

    // Rating and booking count are computed by the database as part of the same statement.
    // A page of twenty services is one query, not twenty-one.
    protected override IQueryable<ServiceResponse> Project(IQueryable<Service> query) =>
        query.Select(s => new ServiceResponse
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            CategoryId = s.CategoryId,
            CategoryName = s.Category.Name,
            Price = s.Price,
            EstimatedDurationMinutes = s.EstimatedDurationMinutes,
            ImageUrl = s.ImagePath == null ? null : "/api/services/" + s.Id + "/image",
            RecommendedMileageIntervalKm = s.RecommendedMileageIntervalKm,
            RecommendedMonthsInterval = s.RecommendedMonthsInterval,
            MinVehicleAgeYears = s.MinVehicleAgeYears,
            MaxVehicleAgeYears = s.MaxVehicleAgeYears,
            IsActive = s.IsActive,
            AverageRating = s.Reviews.Where(r => r.IsVisible).Average(r => (double?)r.Rating),
            ReviewCount = s.Reviews.Count(r => r.IsVisible),
            BookingCount = s.AppointmentItems.Count,
            Tags = s.TagLinks
                .OrderBy(l => l.ServiceTag.Name)
                .Select(l => new NamedItem(l.ServiceTagId, l.ServiceTag.Name))
                .ToList(),
            EngineTypes = s.EngineTypeLinks
                .OrderBy(l => l.EngineType.Name)
                .Select(l => new NamedItem(l.EngineTypeId, l.EngineType.Name))
                .ToList(),
        });

    protected override IQueryable<Service> ForUpdate(IQueryable<Service> query) =>
        query.Include(s => s.TagLinks).Include(s => s.EngineTypeLinks);

    protected override Service Build(ServiceRequest request)
    {
        var service = new Service();
        Apply(service, request);
        return service;
    }

    protected override void Apply(Service entity, ServiceRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = request.Description.Trim();
        entity.CategoryId = request.CategoryId;
        entity.Price = request.Price;
        entity.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        entity.RecommendedMileageIntervalKm = request.RecommendedMileageIntervalKm;
        entity.RecommendedMonthsInterval = request.RecommendedMonthsInterval;
        entity.MinVehicleAgeYears = request.MinVehicleAgeYears;
        entity.MaxVehicleAgeYears = request.MaxVehicleAgeYears;
        entity.IsActive = request.IsActive;

        var tagIds = request.TagIds.Distinct().ToList();
        var engineTypeIds = request.EngineTypeIds.Distinct().ToList();

        // Links are reconciled rather than wiped and rewritten, so a tag that stays keeps the
        // weight the recommender uses for it.
        foreach (var removed in entity.TagLinks.Where(l => !tagIds.Contains(l.ServiceTagId)).ToList())
        {
            entity.TagLinks.Remove(removed);
        }

        foreach (var tagId in tagIds.Where(id => entity.TagLinks.All(l => l.ServiceTagId != id)))
        {
            entity.TagLinks.Add(new ServiceTagLink { ServiceTagId = tagId });
        }

        foreach (var removed in entity.EngineTypeLinks.Where(l => !engineTypeIds.Contains(l.EngineTypeId)).ToList())
        {
            entity.EngineTypeLinks.Remove(removed);
        }

        foreach (var engineTypeId in engineTypeIds.Where(id => entity.EngineTypeLinks.All(l => l.EngineTypeId != id)))
        {
            entity.EngineTypeLinks.Add(new ServiceEngineTypeLink { EngineTypeId = engineTypeId });
        }
    }

    // A booked service is part of somebody's appointment history and of the revenue reports,
    // so it stays. Switching it to inactive takes it out of the catalogue without touching either.
    protected override async Task<string?> GetDeleteBlockerAsync(Service entity, CancellationToken ct)
    {
        var bookings = await Db.Set<AppointmentItem>()
            .IgnoreQueryFilters()
            .CountAsync(i => i.ServiceId == entity.Id, ct);

        return bookings == 0
            ? null
            : $"Usluga se ne može obrisati jer se nalazi na {bookings} rezervacija. Označite je kao neaktivnu da se više ne nudi.";
    }

    protected override async Task ValidateAsync(ServiceRequest request, Service? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var name = request.Name.Trim();
        var id = existing?.Id;

        if (await Db.Set<Service>().AnyAsync(s => s.Name == name && s.Id != id, ct))
        {
            errors["name"] = ["Usluga sa ovim nazivom već postoji."];
        }

        if (!await Db.Set<ServiceCategory>().AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            errors["categoryId"] = ["Izabrana kategorija ne postoji."];
        }

        var tagIds = request.TagIds.Distinct().ToList();

        if (await Db.Set<ServiceTag>().CountAsync(t => tagIds.Contains(t.Id), ct) != tagIds.Count)
        {
            errors["tagIds"] = ["Jedna ili više izabranih oznaka ne postoji."];
        }

        var engineTypeIds = request.EngineTypeIds.Distinct().ToList();

        if (await Db.Set<EngineType>().CountAsync(e => engineTypeIds.Contains(e.Id), ct) != engineTypeIds.Count)
        {
            errors["engineTypeIds"] = ["Jedan ili više izabranih tipova motora ne postoji."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
