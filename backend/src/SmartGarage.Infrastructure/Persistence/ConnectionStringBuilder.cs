using Microsoft.Extensions.Configuration;

namespace SmartGarage.Infrastructure.Persistence;

/// <summary>
/// Builds the SQL Server connection string from the individual values in <c>.env</c>.
/// <para>
/// The connection string is assembled here rather than written out as one long value because
/// the host, port, database name and credentials each have to be settable on their own - Docker
/// Compose supplies a container host name while a developer running from Visual Studio supplies
/// a local instance. One string with everything baked in would have to be edited by hand for
/// each, which is exactly what the instructions forbid.
/// </para>
/// </summary>
public static class ConnectionStringBuilder
{
    /// <summary>
    /// Produces the connection string for the configured database.
    /// <para>
    /// Two shapes are supported, chosen by <c>DB_TRUSTED_CONNECTION</c>:
    /// Windows authentication against a local SQL Server instance, or SQL authentication
    /// against the Docker container. Both are legitimate ways to run this project, and which
    /// one is in use is a configuration decision, not a code change.
    /// </para>
    /// </summary>
    public static string Build(IConfiguration configuration)
    {
        var host = configuration["DB_HOST"] ?? "localhost";
        var port = configuration["DB_PORT"] ?? "1433";
        var database = configuration["DB_NAME"]
            ?? throw new InvalidOperationException(
                "DB_NAME is not set. It must be the student index number without the IB "
                + "prefix - see .env.example.");

        var trusted = string.Equals(
            configuration["DB_TRUSTED_CONNECTION"], "true", StringComparison.OrdinalIgnoreCase);

        if (trusted)
        {
            // Local SQL Server or LocalDB with Windows authentication. The port is not used:
            // a named instance is addressed as HOST\INSTANCE through DB_HOST.
            return
                $"Server={host};"
                + $"Database={database};"
                + "Trusted_Connection=True;"
                + "TrustServerCertificate=True;"
                + "MultipleActiveResultSets=True;";
        }

        var user = configuration["DB_USER"] ?? "sa";
        var password = configuration["MSSQL_SA_PASSWORD"]
            ?? throw new InvalidOperationException(
                "MSSQL_SA_PASSWORD is not set, and DB_TRUSTED_CONNECTION is not true. Set one "
                + "or the other in .env.");

        return
            $"Server={host},{port};"
            + $"Database={database};"
            + $"User Id={user};"
            + $"Password={password};"
            + "TrustServerCertificate=True;"
            + "MultipleActiveResultSets=True;";
    }
}
