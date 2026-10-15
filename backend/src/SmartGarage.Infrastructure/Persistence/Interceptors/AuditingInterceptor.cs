using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Common;

namespace SmartGarage.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps created and modified information onto every <see cref="IAuditable"/> entity as it is
/// saved.
/// <para>
/// Doing this in one interceptor rather than in each service is what makes the audit trail
/// trustworthy: there is no code path that can forget. The instructions require records to say
/// who changed them and when.
/// </para>
/// </summary>
public sealed class AuditingInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public AuditingInterceptor(ICurrentUserService currentUser, IDateTimeProvider clock)
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

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // A creation time that is already set is kept. Only the seeder sets one, to
                    // give demo history its real dates; no request DTO carries this field.
                    if (entry.Entity.CreatedAtUtc == default)
                    {
                        entry.Entity.CreatedAtUtc = now;
                    }

                    entry.Entity.CreatedByUserId ??= userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = now;
                    entry.Entity.ModifiedByUserId = userId;

                    // Creation facts are written once and never rewritten, even if a careless
                    // caller sets them on an update.
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedByUserId)).IsModified = false;
                    break;

                default:
                    break;
            }
        }
    }
}
