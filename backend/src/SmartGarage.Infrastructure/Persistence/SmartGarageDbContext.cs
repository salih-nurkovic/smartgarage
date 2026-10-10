using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartGarage.Application.Abstractions;
using SmartGarage.Domain.Entities;
using SmartGarage.Domain.Entities.Reference;
using SmartGarage.Infrastructure.Persistence.Seeding;

namespace SmartGarage.Infrastructure.Persistence;

/// <summary>
/// The single database context.
/// <para>
/// It lives in Infrastructure and is never referenced from a controller. Controllers depend on
/// Application interfaces only, so "controller must not access DbContext" is a structural fact
/// here rather than a convention someone has to remember.
/// </para>
/// </summary>
public class SmartGarageDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, int>, IAppDbContext
{
    public SmartGarageDbContext(DbContextOptions<SmartGarageDbContext> options)
        : base(options)
    {
    }

    // --- People -------------------------------------------------------------
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<EmployeeSchedule> EmployeeSchedules => Set<EmployeeSchedule>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();

    // --- Vehicles -----------------------------------------------------------
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<EngineType> EngineTypes => Set<EngineType>();

    // --- Catalogue ----------------------------------------------------------
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ServiceTag> ServiceTags => Set<ServiceTag>();
    public DbSet<ServiceTagLink> ServiceTagLinks => Set<ServiceTagLink>();
    public DbSet<ServiceEngineTypeLink> ServiceEngineTypeLinks => Set<ServiceEngineTypeLink>();

    // --- Appointments -------------------------------------------------------
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentItem> AppointmentItems => Set<AppointmentItem>();
    public DbSet<AppointmentStatusHistory> AppointmentStatusHistories => Set<AppointmentStatusHistory>();

    // --- Money --------------------------------------------------------------
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<Refund> Refunds => Set<Refund>();

    // --- Engagement ---------------------------------------------------------
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ServiceReminder> ServiceReminders => Set<ServiceReminder>();

    // --- Recommender --------------------------------------------------------
    public DbSet<UserServiceSignal> UserServiceSignals => Set<UserServiceSignal>();
    public DbSet<RecommendationLog> RecommendationLogs => Set<RecommendationLog>();

    // --- Operations ---------------------------------------------------------
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // The retrying execution strategy refuses a transaction it did not start itself,
        // because it could not replay one. Handing it the whole unit of work is the supported
        // way to have both retries and an explicit transaction.
        var strategy = Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            async token =>
            {
                await using var transaction = await Database.BeginTransactionAsync(token);

                // An exception leaves the transaction uncommitted, and disposing it rolls back.
                var result = await operation(token);
                await transaction.CommitAsync(token);
                return result;
            },
            cancellationToken);
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            async token =>
            {
                await operation(token);
                return true;
            },
            cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Every entity is configured by its own IEntityTypeConfiguration class. Nothing is
        // configured by attribute, so all foreign keys, indexes and delete behaviours are in
        // one predictable place, as the instructions require.
        builder.ApplyConfigurationsFromAssembly(typeof(SmartGarageDbContext).Assembly);

        // Reference data lives in the migration itself, so a fresh database comes up with
        // brands, models, engine types, categories and tags already present and with stable
        // ids that the rest of the seed data can point at.
        ReferenceDataSeeder.Seed(builder);

        ApplySoftDeleteQueryFilters(builder);
    }

    /// <summary>
    /// Hides archived rows from every query automatically.
    /// <para>
    /// Written out explicitly rather than through reflection so the relationships between
    /// filters stay visible. EF warns when a required navigation points at a filtered entity
    /// from an unfiltered one, and seeing the filters listed together is what makes those
    /// warnings easy to reason about.
    /// </para>
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Vehicle>().HasQueryFilter(x => !x.IsDeleted && !x.Owner.IsDeleted);

        builder.Entity<ServiceCategory>().HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<Service>().HasQueryFilter(x => !x.IsDeleted && !x.Category.IsDeleted);

        builder.Entity<Appointment>().HasQueryFilter(
            x => !x.IsDeleted && !x.Customer.IsDeleted && !x.Vehicle.IsDeleted);

        // Dependents repeat their parent's filter. Without this EF cannot compose a join
        // between a filtered and an unfiltered entity and complains at model build time.
        builder.Entity<AppointmentItem>().HasQueryFilter(
            x => !x.Appointment.IsDeleted && !x.Service.IsDeleted);

        builder.Entity<AppointmentStatusHistory>().HasQueryFilter(
            x => !x.Appointment.IsDeleted);

        builder.Entity<Payment>().HasQueryFilter(
            x => !x.Appointment.IsDeleted && !x.Payer.IsDeleted);

        builder.Entity<Refund>().HasQueryFilter(
            x => !x.Payment.Appointment.IsDeleted);

        builder.Entity<Review>().HasQueryFilter(
            x => !x.Appointment.IsDeleted && !x.Service.IsDeleted && !x.Author.IsDeleted);

        builder.Entity<Notification>().HasQueryFilter(
            x => !x.IsDeleted && !x.User.IsDeleted);

        builder.Entity<ServiceReminder>().HasQueryFilter(
            x => !x.Vehicle.IsDeleted && !x.Service.IsDeleted);

        builder.Entity<UserServiceSignal>().HasQueryFilter(
            x => !x.User.IsDeleted && !x.Service.IsDeleted);

        builder.Entity<RecommendationLog>().HasQueryFilter(
            x => !x.User.IsDeleted && !x.Vehicle.IsDeleted && !x.Service.IsDeleted);

        builder.Entity<ServiceTagLink>().HasQueryFilter(x => !x.Service.IsDeleted);
        builder.Entity<ServiceEngineTypeLink>().HasQueryFilter(x => !x.Service.IsDeleted);

        builder.Entity<EmployeeProfile>().HasQueryFilter(x => !x.User.IsDeleted);
        builder.Entity<EmployeeSchedule>().HasQueryFilter(x => !x.EmployeeProfile.User.IsDeleted);
        builder.Entity<RefreshToken>().HasQueryFilter(x => !x.User.IsDeleted);
        builder.Entity<PasswordResetCode>().HasQueryFilter(x => !x.User.IsDeleted);
    }
}
