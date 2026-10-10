using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Application.Common;
using SmartGarage.Domain.Common;
using SmartGarage.Domain.Exceptions;

namespace SmartGarage.Application.Services;

public interface ICrudService<TResponse, in TSearch, in TRequest>
{
    Task<PagedResult<TResponse>> GetAsync(TSearch search, CancellationToken ct);

    Task<TResponse> GetByIdAsync(int id, CancellationToken ct);

    Task<TResponse> CreateAsync(TRequest request, CancellationToken ct);

    Task<TResponse> UpdateAsync(int id, TRequest request, CancellationToken ct);

    /// <summary>Deletes or archives the record and returns a sentence saying which happened.</summary>
    Task<MessageResponse> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>
/// Paging, lookup, save and delete written once for every simple entity.
/// <para>
/// A concrete service states only what is particular to its entity: how to filter and sort
/// it, what it looks like to a client, which rules a write must pass and what stops it from
/// being deleted. Everything that would otherwise be copied between ten services lives here.
/// </para>
/// </summary>
public abstract class CrudService<TEntity, TResponse, TSearch, TRequest>
    : ICrudService<TResponse, TSearch, TRequest>
    where TEntity : BaseEntity
    where TSearch : BaseSearchRequest
{
    protected CrudService(IAppDbContext db)
    {
        Db = db;
    }

    protected IAppDbContext Db { get; }

    protected abstract string NotFoundMessage { get; }

    protected abstract string DeletedMessage { get; }

    /// <summary>The rows the caller may see at all. Overridden to scope by owner.</summary>
    protected virtual IQueryable<TEntity> Query => Db.Set<TEntity>();

    public async Task<PagedResult<TResponse>> GetAsync(TSearch search, CancellationToken ct)
    {
        var filtered = Filter(Query.AsNoTracking(), search);
        var totalCount = await filtered.CountAsync(ct);

        var items = await Project(
                Sort(filtered, search).Skip(search.Skip).Take(search.ClampedPageSize))
            .ToListAsync(ct);

        return new PagedResult<TResponse>(
            items, search.ClampedPage, search.ClampedPageSize, totalCount);
    }

    public async Task<TResponse> GetByIdAsync(int id, CancellationToken ct) =>
        await Project(Query.AsNoTracking().Where(e => e.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException(NotFoundMessage);

    public async Task<TResponse> CreateAsync(TRequest request, CancellationToken ct)
    {
        EnsureCanWrite();
        await ValidateAsync(request, existing: null, ct);

        var entity = Build(request);
        Db.Set<TEntity>().Add(entity);

        await Db.SaveChangesAsync(ct);
        OnChanged();

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<TResponse> UpdateAsync(int id, TRequest request, CancellationToken ct)
    {
        EnsureCanWrite();

        var entity = await LoadAsync(id, ct);
        await ValidateAsync(request, entity, ct);

        Apply(entity, request);

        await Db.SaveChangesAsync(ct);
        OnChanged();

        return await GetByIdAsync(id, ct);
    }

    public async Task<MessageResponse> DeleteAsync(int id, CancellationToken ct)
    {
        EnsureCanWrite();

        var entity = await LoadAsync(id, ct);

        // A record other records depend on is never removed from under them. The caller is
        // told exactly what is in the way, which the instructions require.
        if (await GetDeleteBlockerAsync(entity, ct) is { } blocker)
        {
            throw new ConflictException(blocker);
        }

        var message = await RemoveAsync(entity, ct);
        OnChanged();

        return new MessageResponse(message);
    }

    protected abstract IQueryable<TEntity> Filter(IQueryable<TEntity> query, TSearch search);

    protected abstract IOrderedQueryable<TEntity> Sort(IQueryable<TEntity> query, TSearch search);

    /// <summary>Entity to response, as an expression, so only the needed columns are read.</summary>
    protected abstract IQueryable<TResponse> Project(IQueryable<TEntity> query);

    protected abstract TEntity Build(TRequest request);

    protected abstract void Apply(TEntity entity, TRequest request);

    /// <summary>Rules that need the database: uniqueness and that referenced rows exist.</summary>
    protected virtual Task ValidateAsync(TRequest request, TEntity? existing, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>A sentence explaining why the entity cannot be deleted, or null when it can.</summary>
    protected virtual Task<string?> GetDeleteBlockerAsync(TEntity entity, CancellationToken ct) =>
        Task.FromResult<string?>(null);

    /// <summary>Throws when the caller may read this entity but not change it.</summary>
    protected virtual void EnsureCanWrite()
    {
    }

    /// <summary>Runs after every successful write; used to drop cached copies.</summary>
    protected virtual void OnChanged()
    {
    }

    /// <summary>Loads for editing. Overridden when the edit also touches child rows.</summary>
    protected virtual IQueryable<TEntity> ForUpdate(IQueryable<TEntity> query) => query;

    protected virtual async Task<string> RemoveAsync(TEntity entity, CancellationToken ct)
    {
        Db.Set<TEntity>().Remove(entity);
        await Db.SaveChangesAsync(ct);
        return DeletedMessage;
    }

    protected async Task<TEntity> LoadAsync(int id, CancellationToken ct) =>
        await ForUpdate(Query).SingleOrDefaultAsync(e => e.Id == id, ct)
        ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>
    /// The usual ordering for a list a person scrolls: alphabetical, or newest first when the
    /// client asks for it so that a record just added is at the top.
    /// </summary>
    protected static IOrderedQueryable<TEntity> SortByNameOrNewest(
        IQueryable<TEntity> query,
        TSearch search,
        System.Linq.Expressions.Expression<Func<TEntity, string>> name) =>
        string.Equals(search.OrderBy, "newest", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(e => e.Id)
            : search.Descending ? query.OrderByDescending(name) : query.OrderBy(name);
}
