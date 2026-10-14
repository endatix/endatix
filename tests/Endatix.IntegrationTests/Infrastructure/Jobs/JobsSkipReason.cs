namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Why a Jobs integration test skips on a provider other than PostgreSQL.</summary>
/// <remarks>
/// The two reasons are kept apart because they end differently: a test skipped for
/// <see cref="ModuleNotOnThisProvider"/> runs on every provider once the module registers there, while a test
/// skipped for <see cref="PostgreSqlSpecificSql"/> stays on PostgreSQL, and carries the <c>DbSpecific</c> trait
/// that says so.
/// </remarks>
internal static class JobsSkipReason
{
    /// <summary>The test itself is portable; only the Jobs module is not registered on this provider yet.</summary>
    public const string ModuleNotOnThisProvider =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    /// <summary>The test runs SQL, or asserts schema, that only PostgreSQL has.</summary>
    public const string PostgreSqlSpecificSql = "Uses PostgreSQL-specific SQL.";
}
