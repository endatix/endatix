using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>The claim race on SQL Server, where the row lock is held and watched with SQL Server's own tools.</summary>
public sealed partial class BackgroundJobStateRepositoryIntegrationTests
{
    // An update lock lets the claim's read through, under locking reads and row versioning alike, and stops its update.
    private static readonly RowLockProbe SqlServerRowLock = new(
        jobId => $"""SELECT "Id" FROM jobs."BackgroundJobs" WITH (UPDLOCK, ROWLOCK) WHERE "Id" = {jobId}""",
        $"""SELECT CAST(@@SPID AS int) AS "Value" """,
        holder => $"""
            SELECT count(*) AS "Value"
            FROM sys.dm_exec_requests
            WHERE blocking_session_id = {holder} AND command = 'UPDATE'
            """);

    [Fact]
    [Trait("DbSpecific", "SqlServer")]
    public async Task TryClaimAsync_SqlServerAttemptCountChangedSinceRead_ReturnsNull()
    {
        // Arrange — SQL Server-only: WITH (UPDLOCK, ROWLOCK), @@SPID and sys.dm_exec_requests hold the row and watch
        // the claim wait.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, "Uses SQL Server-specific SQL.");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var race = await ClaimWhileAnotherAttemptIsTakenAsync(SqlServerRowLock, cancellationToken);

        // Assert — claiming here would hand this runner an attempt it did not take.
        ShouldHaveLostTheClaim(race);
    }
}
