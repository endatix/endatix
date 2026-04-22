namespace Endatix.Infrastructure.Data.Config;

/// <summary>
/// Well-known values of <c>DbContext.Database.ProviderName</c> for EF Core database providers used by Endatix,
/// plus helpers for comparisons in <see cref="IDatabaseProviderAwareConfiguration"/> implementations.
/// </summary>
public static class EfCoreDatabaseProviders
{
    public const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";

    public const string Npgsql = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public static bool IsSqlServer(string? providerName) =>
        string.Equals(providerName, SqlServer, StringComparison.Ordinal);

    public static bool IsNpgsql(string? providerName) =>
        string.Equals(providerName, Npgsql, StringComparison.Ordinal);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="providerName"/> is one of the relational providers
    /// this assembly models explicitly (SqlServer or Npgsql).
    /// </summary>
    public static bool IsKnownRelational(string? providerName) =>
        IsSqlServer(providerName) || IsNpgsql(providerName);
}
