using System.Runtime.CompilerServices;
using Endatix.IntegrationTests.Shared;
using Xunit.v3;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A test that runs SQL, or asserts schema, that only PostgreSQL has: it carries the <c>DbSpecific=PostgreSql</c>
/// trait, which keeps it out of the SQL Server CI job, and skips on any other provider when run without that filter.
/// </summary>
/// <remarks>
/// One attribute holds both, so a test cannot carry the skip without the trait, or the reverse. The test still names
/// the PostgreSQL-only SQL it relies on in a comment.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class PostgreSqlSpecificFactAttribute : FactAttribute, ITraitAttribute
{
    public PostgreSqlSpecificFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = JobsSkipReason.PostgreSqlSpecificSql;
        SkipType = typeof(PostgreSqlSpecificFactAttribute);
        SkipUnless = nameof(RunsOnPostgreSql);
    }

    /// <summary>Whether this run's database is PostgreSQL, read as the database fixture reads it.</summary>
    public static bool RunsOnPostgreSql =>
        IntegrationDatabaseSettings.FromEnvironment().Provider == TestDatabaseProvider.PostgreSql;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("DbSpecific", "PostgreSql")];
}
