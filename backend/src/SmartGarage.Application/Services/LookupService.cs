using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Application.Dtos.Catalogue;
using SmartGarage.Domain.Entities.Reference;

namespace SmartGarage.Application.Services;

public sealed record LookupBrand(int Id, string Name, IReadOnlyList<NamedItem> Models);

public sealed class LookupResponse
{
    public required IReadOnlyList<LookupBrand> Brands { get; init; }

    public required IReadOnlyList<NamedItem> EngineTypes { get; init; }

    public required IReadOnlyList<NamedItem> ServiceCategories { get; init; }

    public required IReadOnlyList<NamedItem> ServiceTags { get; init; }
}

public interface ILookupService
{
    Task<LookupResponse> GetAsync(CancellationToken ct);
}

/// <summary>
/// Everything a form needs to fill its dropdowns, in one cached response.
/// <para>
/// These lists are read on nearly every screen and change a few times a year, which is the
/// case the instructions mean by "data loaded on every request should be cached". The cache
/// entry is dropped by the reference services whenever one of the lists is edited.
/// </para>
/// </summary>
public sealed class LookupService : ILookupService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;

    public LookupService(IAppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<LookupResponse> GetAsync(CancellationToken ct) =>
        (await _cache.GetOrCreateAsync(
            CacheKeys.Lookup,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await LoadAsync(ct);
            }))!;

    // Only active rows: an inactive brand stays on the vehicles that have it, but is no longer
    // offered for new ones.
    private async Task<LookupResponse> LoadAsync(CancellationToken ct) => new()
    {
        Brands = await _db.Set<VehicleBrand>()
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new LookupBrand(
                b.Id,
                b.Name,
                b.Models
                    .Where(m => m.IsActive)
                    .OrderBy(m => m.Name)
                    .Select(m => new NamedItem(m.Id, m.Name))
                    .ToList()))
            .ToListAsync(ct),

        EngineTypes = await _db.Set<EngineType>()
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new NamedItem(e.Id, e.Name))
            .ToListAsync(ct),

        ServiceCategories = await _db.Set<ServiceCategory>()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new NamedItem(c.Id, c.Name))
            .ToListAsync(ct),

        ServiceTags = await _db.Set<ServiceTag>()
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new NamedItem(t.Id, t.Name))
            .ToListAsync(ct),
    };
}
