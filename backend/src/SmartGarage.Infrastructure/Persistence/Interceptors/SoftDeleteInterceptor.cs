using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Common;

namespace SmartGarage.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Converts a delete of an <see cref="ISoftDeletable"/> entity into an archive.
/// <para>
/// A service that decides an entity has dependents calls <c>Remove</c> as usual; this turns
/// that into a flag update so the row, and everything that references it, stays intact. The
/// instructions require deletion to respect foreign key order and to explain itself rather than
/// fail.
/// </para>
/// <para>
/// Hard deletion is still possible where it is genuinely correct - a vehicle with no history,
/// for instance - because the service clears the entity from the change tracker and issues a
/// direct delete instead.
/// </para>
/// </summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public SoftDeleteInterceptor(ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = now;
            entry.Entity.DeletedByUserId = userId;
        }
    }
}
