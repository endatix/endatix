using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Resolves the configured database provider from connection string settings.
/// </summary>
public static class DatabaseProviderResolver
{
    /// <summary>
    /// Returns <see langword="true"/> when <c>ConnectionStrings:DefaultConnection_DbProvider</c>
    /// is <c>postgresql</c> or <c>postgres</c>; otherwise <see langword="false"/> (SQL Server default).
    /// Missing, empty, and unrecognized values intentionally fall back to SQL Server.
    /// </summary>
    public static bool IsPostgreSql(IConfiguration configuration)
    {
        var providerName = configuration.GetConnectionString("DefaultConnection_DbProvider")?.ToLowerInvariant();

        return providerName is "postgresql" or "postgres";
    }

    /// <summary>
    /// Fails a module that supports only PostgreSQL when the host is configured for another provider. A module calls
    /// it first thing when it registers, which means its feature flag is on: the host asked for the module and is told
    /// at startup that it cannot have it, rather than at the module's first database call.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="moduleName">The module's name as an operator knows it, such as <c>Reporting</c>.</param>
    /// <param name="featureFlag">The feature flag that turns the module on.</param>
    /// <exception cref="InvalidOperationException">The configured provider is not PostgreSQL.</exception>
    public static void RequirePostgreSql(IConfiguration configuration, string moduleName, string featureFlag)
    {
        if (!IsPostgreSql(configuration))
        {
            throw new InvalidOperationException(
                $"The {moduleName} module requires PostgreSQL. Either set the connection string " +
                $"setting 'DefaultConnection_DbProvider' to 'postgresql', or turn off " +
                $"'Endatix:FeatureFlags:{featureFlag}'.");
        }
    }
}
