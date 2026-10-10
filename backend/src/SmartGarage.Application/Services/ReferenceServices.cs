using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Reference;
using SmartGarage.Domain.Common;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Entities.Reference;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

/// <summary>
/// Base for the administrator-managed lookup tables. Every write drops the cached dropdown
/// bundle, so a brand added on the desktop is offered on the mobile app straight away.
/// </summary>
public abstract class ReferenceService<TEntity, TResponse, TSearch, TRequest>
    : CrudService<TEntity, TResponse, TSearch, TRequest>
    where TEntity : BaseEntity
    where TSearch : ReferenceSearchRequest
{
    private readonly IMemoryCache _cache;

    protected ReferenceService(IAppDbContext db, IMemoryCache cache)
        : base(db)
    {
        _cache = cache;
    }

    protected override void OnChanged() => _cache.Remove(CacheKeys.Lookup);

    protected static ValidationException Taken(string field, string message) => new(field, message);
}

public sealed class VehicleBrandService
    : ReferenceService<VehicleBrand, VehicleBrandResponse, ReferenceSearchRequest, VehicleBrandRequest>
{
    public VehicleBrandService(IAppDbContext db, IMemoryCache cache)
        : base(db, cache)
    {
    }

    protected override string NotFoundMessage => "Marka vozila nije pronađena.";

    protected override string DeletedMessage => "Marka vozila je obrisana.";

    protected override IQueryable<VehicleBrand> Filter(
        IQueryable<VehicleBrand> query,
        ReferenceSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(b => b.Name.Contains(term));
        }

        return search.IsActive is { } isActive ? query.Where(b => b.IsActive == isActive) : query;
    }

    protected override IOrderedQueryable<VehicleBrand> Sort(
        IQueryable<VehicleBrand> query,
        ReferenceSearchRequest search) =>
        SortByNameOrNewest(query, search, b => b.Name);

    protected override IQueryable<VehicleBrandResponse> Project(IQueryable<VehicleBrand> query) =>
        query.Select(b => new VehicleBrandResponse
        {
            Id = b.Id,
            Name = b.Name,
            IsActive = b.IsActive,
            ModelCount = b.Models.Count,
        });

    protected override VehicleBrand Build(VehicleBrandRequest request)
    {
        var brand = new VehicleBrand();
        Apply(brand, request);
        return brand;
    }

    protected override void Apply(VehicleBrand entity, VehicleBrandRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
    }

    protected override async Task ValidateAsync(
        VehicleBrandRequest request,
        VehicleBrand? existing,
        CancellationToken ct)
    {
        var name = request.Name.Trim();
        var id = existing?.Id;

        if (await Db.Set<VehicleBrand>().AnyAsync(b => b.Name == name && b.Id != id, ct))
        {
            throw Taken("name", "Marka sa ovim nazivom već postoji.");
        }
    }

    protected override async Task<string?> GetDeleteBlockerAsync(VehicleBrand entity, CancellationToken ct)
    {
        var models = await Db.Set<VehicleModel>().CountAsync(m => m.BrandId == entity.Id, ct);

        return models == 0
            ? null
            : $"Marka se ne može obrisati jer ima {models} modela. Prvo obrišite modele ili marku označite kao neaktivnu.";
    }
}

public sealed class VehicleModelService
    : ReferenceService<VehicleModel, VehicleModelResponse, VehicleModelSearchRequest, VehicleModelRequest>
{
    public VehicleModelService(IAppDbContext db, IMemoryCache cache)
        : base(db, cache)
    {
    }

    protected override string NotFoundMessage => "Model vozila nije pronađen.";

    protected override string DeletedMessage => "Model vozila je obrisan.";

    protected override IQueryable<VehicleModel> Filter(
        IQueryable<VehicleModel> query,
        VehicleModelSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(m => m.Name.Contains(term) || m.Brand.Name.Contains(term));
        }

        if (search.BrandId is { } brandId)
        {
            query = query.Where(m => m.BrandId == brandId);
        }

        return search.IsActive is { } isActive ? query.Where(m => m.IsActive == isActive) : query;
    }

    protected override IOrderedQueryable<VehicleModel> Sort(
        IQueryable<VehicleModel> query,
        VehicleModelSearchRequest search) =>
        SortByNameOrNewest(query, search, m => m.Brand.Name + " " + m.Name);

    protected override IQueryable<VehicleModelResponse> Project(IQueryable<VehicleModel> query) =>
        query.Select(m => new VehicleModelResponse
        {
            Id = m.Id,
            Name = m.Name,
            BrandId = m.BrandId,
            BrandName = m.Brand.Name,
            IsActive = m.IsActive,
            VehicleCount = m.Vehicles.Count,
        });

    protected override VehicleModel Build(VehicleModelRequest request)
    {
        var model = new VehicleModel();
        Apply(model, request);
        return model;
    }

    protected override void Apply(VehicleModel entity, VehicleModelRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.BrandId = request.BrandId;
        entity.IsActive = request.IsActive;
    }

    protected override async Task ValidateAsync(
        VehicleModelRequest request,
        VehicleModel? existing,
        CancellationToken ct)
    {
        var name = request.Name.Trim();
        var id = existing?.Id;

        if (!await Db.Set<VehicleBrand>().AnyAsync(b => b.Id == request.BrandId, ct))
        {
            throw new ValidationException("brandId", "Izabrana marka vozila ne postoji.");
        }

        if (await Db.Set<VehicleModel>()
                .AnyAsync(m => m.BrandId == request.BrandId && m.Name == name && m.Id != id, ct))
        {
            throw Taken("name", "Ova marka već ima model sa tim nazivom.");
        }
    }

    protected override async Task<string?> GetDeleteBlockerAsync(VehicleModel entity, CancellationToken ct)
    {
        // Archived vehicles still point at the model, so they count as well.
        var vehicles = await Db.Set<Vehicle>()
            .IgnoreQueryFilters()
            .CountAsync(v => v.ModelId == entity.Id, ct);

        return vehicles == 0
            ? null
            : $"Model se ne može obrisati jer ga koristi {vehicles} vozila. Označite ga kao neaktivan da se više ne nudi.";
    }
}

public sealed class EngineTypeService
    : ReferenceService<EngineType, EngineTypeResponse, ReferenceSearchRequest, EngineTypeRequest>
{
    public EngineTypeService(IAppDbContext db, IMemoryCache cache)
        : base(db, cache)
    {
    }

    protected override string NotFoundMessage => "Tip motora nije pronađen.";

    protected override string DeletedMessage => "Tip motora je obrisan.";

    protected override IQueryable<EngineType> Filter(
        IQueryable<EngineType> query,
        ReferenceSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.Code.Contains(term));
        }

        return search.IsActive is { } isActive ? query.Where(e => e.IsActive == isActive) : query;
    }

    protected override IOrderedQueryable<EngineType> Sort(
        IQueryable<EngineType> query,
        ReferenceSearchRequest search) =>
        SortByNameOrNewest(query, search, e => e.Name);

    protected override IQueryable<EngineTypeResponse> Project(IQueryable<EngineType> query) =>
        query.Select(e => new EngineTypeResponse
        {
            Id = e.Id,
            Name = e.Name,
            Code = e.Code,
            IsActive = e.IsActive,
        });

    protected override EngineType Build(EngineTypeRequest request)
    {
        var engineType = new EngineType();
        Apply(engineType, request);
        return engineType;
    }

    protected override void Apply(EngineType entity, EngineTypeRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.IsActive = request.IsActive;
    }

    protected override async Task ValidateAsync(
        EngineTypeRequest request,
        EngineType? existing,
        CancellationToken ct)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        var id = existing?.Id;

        var clashes = await Db.Set<EngineType>()
            .Where(e => e.Id != id && (e.Name == name || e.Code == code))
            .Select(e => new { e.Name, e.Code })
            .ToListAsync(ct);

        if (clashes.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw Taken("name", "Tip motora sa ovim nazivom već postoji.");
        }

        if (clashes.Count > 0)
        {
            throw Taken("code", "Tip motora sa ovom šifrom već postoji.");
        }
    }

    protected override async Task<string?> GetDeleteBlockerAsync(EngineType entity, CancellationToken ct)
    {
        var vehicles = await Db.Set<Vehicle>()
            .IgnoreQueryFilters()
            .CountAsync(v => v.EngineTypeId == entity.Id, ct);

        var services = await Db.Set<ServiceEngineTypeLink>()
            .IgnoreQueryFilters()
            .CountAsync(l => l.EngineTypeId == entity.Id, ct);

        return vehicles + services == 0
            ? null
            : $"Tip motora se ne može obrisati jer ga koristi {vehicles} vozila i {services} usluga. Označite ga kao neaktivan da se više ne nudi.";
    }
}

public sealed class ServiceCategoryService
    : ReferenceService<ServiceCategory, ServiceCategoryResponse, ReferenceSearchRequest, ServiceCategoryRequest>
{
    public ServiceCategoryService(IAppDbContext db, IMemoryCache cache)
        : base(db, cache)
    {
    }

    protected override string NotFoundMessage => "Kategorija usluga nije pronađena.";

    protected override string DeletedMessage => "Kategorija usluga je obrisana.";

    protected override IQueryable<ServiceCategory> Filter(
        IQueryable<ServiceCategory> query,
        ReferenceSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Description!.Contains(term));
        }

        return search.IsActive is { } isActive ? query.Where(c => c.IsActive == isActive) : query;
    }

    // Categories have an order the administrator chose; that is the default, not the alphabet.
    protected override IOrderedQueryable<ServiceCategory> Sort(
        IQueryable<ServiceCategory> query,
        ReferenceSearchRequest search) =>
        string.IsNullOrWhiteSpace(search.OrderBy)
            ? query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            : SortByNameOrNewest(query, search, c => c.Name);

    protected override IQueryable<ServiceCategoryResponse> Project(IQueryable<ServiceCategory> query) =>
        query.Select(c => new ServiceCategoryResponse
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            IsActive = c.IsActive,
            ServiceCount = c.Services.Count,
        });

    protected override ServiceCategory Build(ServiceCategoryRequest request)
    {
        var category = new ServiceCategory();
        Apply(category, request);
        return category;
    }

    protected override void Apply(ServiceCategory entity, ServiceCategoryRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
    }

    protected override async Task ValidateAsync(
        ServiceCategoryRequest request,
        ServiceCategory? existing,
        CancellationToken ct)
    {
        var name = request.Name.Trim();
        var id = existing?.Id;

        if (await Db.Set<ServiceCategory>().AnyAsync(c => c.Name == name && c.Id != id, ct))
        {
            throw Taken("name", "Kategorija sa ovim nazivom već postoji.");
        }
    }

    protected override async Task<string?> GetDeleteBlockerAsync(ServiceCategory entity, CancellationToken ct)
    {
        var services = await Db.Set<Service>().CountAsync(s => s.CategoryId == entity.Id, ct);

        return services == 0
            ? null
            : $"Kategorija se ne može obrisati jer sadrži {services} usluga. Prvo ih premjestite u drugu kategoriju.";
    }
}

public sealed class ServiceTagService
    : ReferenceService<ServiceTag, ServiceTagResponse, ReferenceSearchRequest, ServiceTagRequest>
{
    public ServiceTagService(IAppDbContext db, IMemoryCache cache)
        : base(db, cache)
    {
    }

    protected override string NotFoundMessage => "Oznaka usluge nije pronađena.";

    protected override string DeletedMessage => "Oznaka usluge je obrisana.";

    protected override IQueryable<ServiceTag> Filter(
        IQueryable<ServiceTag> query,
        ReferenceSearchRequest search)
    {
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            query = query.Where(t => t.Name.Contains(term) || t.Slug.Contains(term));
        }

        return search.IsActive is { } isActive ? query.Where(t => t.IsActive == isActive) : query;
    }

    protected override IOrderedQueryable<ServiceTag> Sort(
        IQueryable<ServiceTag> query,
        ReferenceSearchRequest search) =>
        SortByNameOrNewest(query, search, t => t.Name);

    protected override IQueryable<ServiceTagResponse> Project(IQueryable<ServiceTag> query) =>
        query.Select(t => new ServiceTagResponse
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            IsActive = t.IsActive,
            ServiceCount = t.ServiceLinks.Count,
        });

    protected override ServiceTag Build(ServiceTagRequest request)
    {
        var tag = new ServiceTag();
        Apply(tag, request);
        return tag;
    }

    protected override void Apply(ServiceTag entity, ServiceTagRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.Slug = request.Slug.Trim();
        entity.IsActive = request.IsActive;
    }

    protected override async Task ValidateAsync(
        ServiceTagRequest request,
        ServiceTag? existing,
        CancellationToken ct)
    {
        var name = request.Name.Trim();
        var slug = request.Slug.Trim();
        var id = existing?.Id;

        var clashes = await Db.Set<ServiceTag>()
            .Where(t => t.Id != id && (t.Name == name || t.Slug == slug))
            .Select(t => new { t.Name, t.Slug })
            .ToListAsync(ct);

        if (clashes.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw Taken("name", "Oznaka sa ovim nazivom već postoji.");
        }

        if (clashes.Count > 0)
        {
            throw Taken("slug", "Oznaka sa ovim identifikatorom već postoji.");
        }
    }

    protected override async Task<string?> GetDeleteBlockerAsync(ServiceTag entity, CancellationToken ct)
    {
        var services = await Db.Set<ServiceTagLink>()
            .IgnoreQueryFilters()
            .CountAsync(l => l.ServiceTagId == entity.Id, ct);

        return services == 0
            ? null
            : $"Oznaka se ne može obrisati jer je koristi {services} usluga. Označite je kao neaktivnu da se više ne nudi.";
    }
}
