namespace SmartGarage.Infrastructure.Configuration;

/// <summary>
/// Loads the solution-level <c>.env</c> file into environment variables so that a single
/// file is the one source of configuration for every way the application can be started:
/// Visual Studio, <c>dotnet run</c>, and Docker Compose.
/// <para>
/// Inside a container Compose has already injected the same values through <c>env_file</c>,
/// so nothing is found and nothing is overwritten. Values that already exist are never
/// replaced, which means a real environment variable always wins over the file.
/// </para>
/// <para>
/// This runs exactly once, at startup, before the configuration system is built. Nothing
/// re-reads environment variables per request; options are bound once and injected as
/// <c>IOptions&lt;T&gt;</c> (course instructions, section 8.2).
/// </para>
/// </summary>
public static class DotEnvLoader
{
    private const int MaxParentDirectoriesToSearch = 8;

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a <c>.env</c> file and
    /// loads it. Returns the path that was loaded, or <c>null</c> when no file was found.
    /// </summary>
    public static string? Load(string? startDirectory = null)
    {
        var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());

        for (var depth = 0; directory is not null && depth < MaxParentDirectoriesToSearch; depth++)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                Apply(File.ReadAllLines(candidate));
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void Apply(IReadOnlyList<string> lines)
    {
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            // Ordinal: parsiranje konfiguracije ne smije ovisiti o lokalizaciji racunara.
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // Allow optional surrounding quotes: KEY="value with spaces"
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // A value already present in the real environment always wins, so Docker Compose
            // and CI can override the file without editing it.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
