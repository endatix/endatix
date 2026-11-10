using Endatix.IntegrationTests;
using Endatix.IntegrationTests.Shared;
using Xunit.Sdk;
using Xunit.v3;

[assembly: TestCollectionOrderer(typeof(SlowestCollectionsFirst))]

namespace Endatix.IntegrationTests;

// The integration-test collections. Collections run in parallel (xunit.runner.json), the classes in one collection
// run one after another, and each collection gets its own fixture, so its own host and its own database.
//
// A new class picks its collection by its CI lane (tests/README.md, Lanes) and the fixture it needs:
//
//   Lane  | Web host (EndatixIntegrationWebHostFixture) | Database only (DbIntegrationFixture)
//   Rest  | EndatixIntegrationTestCollection            | DbIntegrationTestCollection
//   Jobs  | JobsHostTestCollection                      | JobsDbTestCollection
//
// The other collections hold one slow class, or a group, so that no collection outlasts its lane's share of the
// test time, and start first (SlowestCollectionsFirst). Move a class only to rebalance, by the times in the lane's
// test-durations-* artifact (tests/README.md, Collections). A test class with no [Collection] is a collection of its own.

/// <summary>Rest lane, web host: HTTP and API tests.</summary>
[CollectionDefinition(nameof(EndatixIntegrationTestCollection))]
public sealed class EndatixIntegrationTestCollection : ICollectionFixture<EndatixIntegrationWebHostFixture>;

/// <summary>Rest lane, web host: <c>AudienceDirectoryFlowTests</c> alone, about half the lane's test time.</summary>
[CollectionDefinition(nameof(AudienceDirectoryTestCollection))]
public sealed class AudienceDirectoryTestCollection : ICollectionFixture<EndatixIntegrationWebHostFixture>;

/// <summary>Rest lane, database only: migrations, EF and query filters without a full host.</summary>
[CollectionDefinition(nameof(DbIntegrationTestCollection))]
public sealed class DbIntegrationTestCollection : ICollectionFixture<DbIntegrationFixture>;

/// <summary>Rest lane, database only: the jobs classes that run in Rest to keep the SQL Server lanes even.</summary>
[CollectionDefinition(nameof(RestLaneJobsTestCollection))]
public sealed class RestLaneJobsTestCollection : ICollectionFixture<DbIntegrationFixture>;

/// <summary>Jobs lane, web host: background work, the outbox and the jobs API on the shared host.</summary>
[CollectionDefinition(nameof(JobsHostTestCollection))]
public sealed class JobsHostTestCollection : ICollectionFixture<EndatixIntegrationWebHostFixture>;

/// <summary>Jobs lane, database only: enqueueing, deduplication, outbox handlers and webhook delivery.</summary>
[CollectionDefinition(nameof(JobsDbTestCollection))]
public sealed class JobsDbTestCollection : ICollectionFixture<DbIntegrationFixture>;

/// <summary>Jobs lane, database only: the job store, its schema, scheduler nodes and job types.</summary>
[CollectionDefinition(nameof(JobStoreTestCollection))]
public sealed class JobStoreTestCollection : ICollectionFixture<DbIntegrationFixture>;

/// <summary>Jobs lane, database only: <c>JobsSchedulerStartupTests</c> alone, the lane's slowest class.</summary>
[CollectionDefinition(nameof(JobsSchedulerStartupTestCollection))]
public sealed class JobsSchedulerStartupTestCollection : ICollectionFixture<DbIntegrationFixture>;

/// <summary>Jobs lane, database only: <c>BackgroundJobExecutionIntegrationTests</c> alone.</summary>
[CollectionDefinition(nameof(JobExecutionTestCollection))]
public sealed class JobExecutionTestCollection : ICollectionFixture<DbIntegrationFixture>;

/// <summary>
/// Starts the collections that hold one slow class first, so none of them waits for a thread behind shorter ones and
/// lengthens its lane. The rest keep xUnit's default order.
/// </summary>
public sealed class SlowestCollectionsFirst : ITestCollectionOrderer
{
    private static readonly string[] _slowest =
    [
        nameof(AudienceDirectoryTestCollection),
        nameof(JobsSchedulerStartupTestCollection),
        nameof(JobExecutionTestCollection),
    ];

    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(
        IReadOnlyCollection<TTestCollection> testCollections)
        where TTestCollection : ITestCollection =>
        [.. DefaultTestCollectionOrderer.Instance.OrderTestCollections(testCollections)
            .OrderBy(collection => Rank(collection.TestCollectionDisplayName))];

    private static int Rank(string displayName) =>
        Array.IndexOf(_slowest, displayName) is var index and >= 0 ? index : _slowest.Length;
}
