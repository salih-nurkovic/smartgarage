using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SmartGarage.Infrastructure.Configuration;

namespace SmartGarage.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations</c> build a context without starting the whole application.
/// <para>
/// Without this, the EF tools would have to construct the API host, which would try to reach
/// RabbitMQ and SQL Server just to work out what a migration should contain. The factory reads
/// the same <c>.env</c> everything else reads, so the migration is generated against the same
/// configuration the application runs with.
/// </para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartGarageDbContext>
{
    public SmartGarageDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.Load(Directory.GetCurrentDirectory());

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<SmartGarageDbContext>()
            .UseSqlServer(ConnectionStringBuilder.Build(configuration))
            .Options;

        // No interceptors here on purpose: auditing and soft delete are runtime behaviour and
        // have nothing to say about schema generation.
        return new SmartGarageDbContext(options);
    }
}
