using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartGarage.Application.Abstractions;
using SmartGarage.Infrastructure.Configuration;
using SmartGarage.Infrastructure.Persistence;
using SmartGarage.Infrastructure.Persistence.Seeding;
using Xunit;

namespace SmartGarage.IntegrationTests.Support;

/// <summary>
/// Boots the real API against a real SQL Server database of its own.
/// <para>
/// An in-memory provider would accept things SQL Server rejects - it has no transactions and
/// does not enforce unique indexes - and those are exactly the behaviours the authentication
/// code depends on. So the tests use the server from <c>.env</c>, but a separate database
/// that is dropped and rebuilt from the migrations on every run. The development database is
/// never touched.
/// </para>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestDatabaseName = "220014_tests";

    // Uploads made by tests go to a throwaway folder, never into the source tree.
    private readonly string _uploadsRoot =
        Path.Combine(Path.GetTempPath(), $"smartgarage-tests-{Guid.NewGuid():N}");

    public ApiFactory()
    {
        // Set before the host starts: the .env loader never overwrites a variable that
        // already exists, so these two win over the file.
        Environment.SetEnvironmentVariable("DB_NAME", TestDatabaseName);
        Environment.SetEnvironmentVariable("SEED_DATA", "false");
        Environment.SetEnvironmentVariable("UPLOADS_ROOT", _uploadsRoot);

        DotEnvLoader.Load(AppContext.BaseDirectory);
    }

    public TestClock Clock { get; } = new();

    public CapturingEventPublisher Events { get; } = new();

    public async Task InitializeAsync()
    {
        await DropTestDatabaseAsync();

        // Resolving a service starts the host, which applies the migrations.
        await using var scope = Services.CreateAsyncScope();

        // Accounts and the catalogue are seeded. The 80-second appointment history is not:
        // no test here needs it, and it would make every run that much slower.
        await scope.ServiceProvider
            .GetRequiredService<IdentitySeeder>()
            .SeedAsync(DateTime.UtcNow, CancellationToken.None);
        await scope.ServiceProvider
            .GetRequiredService<CatalogueSeeder>()
            .SeedAsync(DateTime.UtcNow, CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await DropTestDatabaseAsync();

        if (Directory.Exists(_uploadsRoot))
        {
            Directory.Delete(_uploadsRoot, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(Clock);

            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher>(Events);
        });

    private static async Task DropTestDatabaseAsync()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var options = new DbContextOptionsBuilder<SmartGarageDbContext>()
            .UseSqlServer(ConnectionStringBuilder.Build(configuration))
            .Options;

        await using var db = new SmartGarageDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}

/// <summary>Every test class shares one host and one database, and runs one at a time.</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
