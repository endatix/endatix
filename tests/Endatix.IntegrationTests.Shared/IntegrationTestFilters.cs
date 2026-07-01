namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// xUnit filter expressions for <c>dotnet test --filter</c>. Keep CI workflows in sync.
/// </summary>
/// <remarks>
/// One test process = one Testcontainers database. CI uses a provider matrix (two jobs).
/// Provider-agnostic tests have no <c>DbSpecific</c> trait and run in both jobs.
/// </remarks>
public static class IntegrationTestFilters
{
    /// <summary>PostgreSQL CI leg and local default (excludes Keycloak and SQL Server–only tests).</summary>
    public const string Default = "Category!=Keycloak&DbSpecific!=SqlServer";

    /// <summary>PostgreSQL PR fast gate (P0 + P1).</summary>
    public const string PrFast = "Category!=Keycloak&Priority!=P2&DbSpecific!=SqlServer";

    /// <summary>SQL Server CI leg (excludes Keycloak and PostgreSQL–only tests).</summary>
    public const string SqlServer = "Category!=Keycloak&DbSpecific!=PostgreSql";
}
