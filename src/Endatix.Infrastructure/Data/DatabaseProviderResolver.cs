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
}
