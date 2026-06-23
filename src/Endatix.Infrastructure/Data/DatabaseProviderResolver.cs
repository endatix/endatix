using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Resolves the configured database provider from connection string settings.
/// </summary>
public static class DatabaseProviderResolver
{
    public static bool IsPostgreSql(IConfiguration configuration)
    {
        var providerName = configuration.GetConnectionString("DefaultConnection_DbProvider")?.ToLowerInvariant();

        return providerName is "postgresql" or "postgres";
    }
}
