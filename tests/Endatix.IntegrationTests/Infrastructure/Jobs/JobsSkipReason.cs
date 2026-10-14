namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Why a Jobs integration test skips on a provider other than PostgreSQL.</summary>
/// <remarks>
/// The Jobs module runs on every provider, so a test skips only for what it asserts: such a test also carries the
/// <c>DbSpecific</c> trait, and a comment naming the PostgreSQL-only SQL.
/// </remarks>
internal static class JobsSkipReason
{
    /// <summary>The test runs SQL, or asserts schema, that only PostgreSQL has.</summary>
    public const string PostgreSqlSpecificSql = "Uses PostgreSQL-specific SQL.";
}
