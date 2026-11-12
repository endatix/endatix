using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// A jobs node takes its Snowflake worker id from <c>Endatix:Data:SnowflakeWorkerId</c>. The node is built but never
/// started, so no database has to answer.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P2")]
[Trait("Lane", "Jobs")]
public sealed class JobsWorkerIdTests
{
    // IdGen's default structure: 41 timestamp bits, 10 worker bits, 12 sequence bits.
    private const int SEQUENCE_BITS = 12;
    private const int WORKER_MASK = (1 << 10) - 1;

    [Fact]
    public async Task Node_MintsIdsWithTheConfiguredWorkerId()
    {
        await using var node = JobsTestNode.Create(
            new JobsTestNodeDatabase(TestDatabaseProvider.PostgreSql, "Host=unused;Database=unused"),
            new Dictionary<string, string?> { ["Endatix:Data:SnowflakeWorkerId"] = "7" });

        var id = node.Services.GetRequiredService<IIdGenerator<long>>().CreateId();

        ((id >> SEQUENCE_BITS) & WORKER_MASK).Should().Be(7);
    }
}
