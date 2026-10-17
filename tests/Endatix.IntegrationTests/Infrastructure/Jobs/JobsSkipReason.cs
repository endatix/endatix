namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Why a Jobs integration test skips on a provider other than PostgreSQL.</summary>
/// <remarks>
/// The Jobs module runs on every provider, so a test skips only for what it asserts. Such a test is marked
/// <see cref="PostgreSqlSpecificFactAttribute"/>, which carries this skip together with the <c>DbSpecific</c> trait,
/// and names the PostgreSQL-only SQL in a comment.
/// </remarks>
internal static class JobsSkipReason
{
    /// <summary>The test runs SQL, or asserts schema, that only PostgreSQL has.</summary>
    public const string PostgreSqlSpecificSql = "Uses PostgreSQL-specific SQL.";
}
