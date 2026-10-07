using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartGarage.Application.Abstractions;

namespace SmartGarage.Infrastructure.Persistence.Seeding;

/// <summary>
/// Brings the database up at startup: applies migrations, then seeds if asked to.
/// <para>
/// Only the API runs this. The Worker waits for the API to report healthy before it starts, so
/// two processes can never migrate the same database at once.
/// </para>
/// <para>
/// Seeding is idempotent at every step - each seeder checks whether its own data already exists
/// and returns if it does. Restarting the container therefore does not duplicate anything,
/// which matters because <c>docker compose up</c> is expected to work the second time as well
/// as the first.
/// </para>
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseInitializer));
        var configuration = provider.GetRequiredService<IConfiguration>();
        var db = provider.GetRequiredService<SmartGarageDbContext>();

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(ct);
        logger.LogInformation("Migrations applied.");

        if (!configuration.GetValue("SEED_DATA", false))
        {
            logger.LogInformation("SEED_DATA is not true - skipping seed data.");
            return;
        }

        var clock = provider.GetRequiredService<IDateTimeProvider>();
        var nowUtc = clock.UtcNow;

        // Order matters: users before vehicles, catalogue before appointments. Each seeder
        // depends on the one above it having finished.
        await provider.GetRequiredService<IdentitySeeder>().SeedAsync(nowUtc, ct);
        await provider.GetRequiredService<CatalogueSeeder>().SeedAsync(nowUtc, ct);
        await provider.GetRequiredService<DemoDataSeeder>().SeedAsync(nowUtc, ct);

        logger.LogInformation("Seeding complete.");
    }
}
