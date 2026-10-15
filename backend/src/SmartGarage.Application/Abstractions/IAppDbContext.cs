using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartGarage.Domain.Entities;

namespace SmartGarage.Application.Abstractions;

/// <summary>
/// What the service layer is allowed to see of the database.
/// <para>
/// Services depend on this interface, and the concrete context stays in Infrastructure. That
/// keeps the chain the instructions prescribe - controller, then service, then DbContext -
/// while the Application project never references the persistence project.
/// </para>
/// </summary>
public interface IAppDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<ApplicationRole> Roles { get; }

    DbSet<IdentityUserRole<int>> UserRoles { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<PasswordResetCode> PasswordResetCodes { get; }

    DbSet<ErrorLog> ErrorLogs { get; }

    /// <summary>Any other entity set, so this interface does not grow a property per table.</summary>
#pragma warning disable CA1716 // The name is DbContext's own; matching it is what lets the context implement this.
    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;
#pragma warning restore CA1716

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an operation that saves more than once inside one explicit transaction, which the
    /// instructions require. The operation may run again if the connection drops, so it must
    /// not have side effects outside the database.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same, at a chosen isolation level. Booking uses <see cref="IsolationLevel.Serializable"/>
    /// so that two requests for the last free place cannot both read "free" and both insert.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets every tracked entity. An operation that may be retried calls this first, so a
    /// second attempt does not save what the first one had already added.
    /// </summary>
    void DiscardChanges();

    /// <inheritdoc cref="ExecuteInTransactionAsync{T}(Func{CancellationToken, Task{T}}, CancellationToken)"/>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
