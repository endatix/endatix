using Ardalis.GuardClauses;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Builds <see cref="IConfiguration"/> for EF Core design-time tooling (dotnet ef).
/// </summary>
public static class ModuleDesignTimeConfiguration
{
    /// <summary>
    /// Loads appsettings and environment variables from the current working directory (typically the startup project when using <c>--startup-project</c>).
    /// </summary>
    public static IConfiguration Build()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Returns <c>ConnectionStrings:DefaultConnection</c> or throws when missing.
    /// </summary>
    public static string GetDefaultConnectionString(IConfiguration configuration)
    {
        Guard.Against.Null(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        Guard.Against.NullOrEmpty(connectionString, "DefaultConnection");

        return connectionString;
    }
}
