using Endatix.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Tests.Data;

public class SnowflakeIdGeneratorTests
{
    // IdGen's default structure: 41 timestamp bits, 10 worker bits, 12 sequence bits.
    private const int SEQUENCE_BITS = 12;
    private const int WORKER_MASK = (1 << 10) - 1;

    [Fact]
    public void CreateId_WorkerFromOptions_SetsWorkerBits()
    {
        var generator = new SnowflakeIdGenerator(Options.Create(new DataOptions { SnowflakeWorkerId = 5 }));

        var worker = WorkerId(generator.CreateId());

        worker.Should().Be(5);
    }

    [Fact]
    public void CreateId_SameWorker_SharesOneSequence()
    {
        var options = Options.Create(new DataOptions { SnowflakeWorkerId = 8 });
        var first = new SnowflakeIdGenerator(options);
        var second = new SnowflakeIdGenerator(options);
        var ids = new HashSet<long>();

        for (var i = 0; i < 500; i++)
        {
            ids.Add(first.CreateId());
            ids.Add(second.CreateId());
        }

        ids.Should().HaveCount(1000);
    }

    [Fact]
    public void CreateId_DifferentWorkers_SetDifferentWorkerBits()
    {
        var workerOne = new SnowflakeIdGenerator(Options.Create(new DataOptions { SnowflakeWorkerId = 1 }));
        var workerTwo = new SnowflakeIdGenerator(Options.Create(new DataOptions { SnowflakeWorkerId = 2 }));

        WorkerId(workerOne.CreateId()).Should().Be(1);
        WorkerId(workerTwo.CreateId()).Should().Be(2);
    }

    [Fact]
    public void CreateId_WithinOneWorker_IncreasesStrictly()
    {
        var generator = new SnowflakeIdGenerator(Options.Create(new DataOptions { SnowflakeWorkerId = 11 }));
        var previous = generator.CreateId();

        for (var i = 0; i < 1000; i++)
        {
            var next = generator.CreateId();
            next.Should().BeGreaterThan(previous);
            previous = next;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    public void CreateId_WorkerAtTheEdges_SetsWorkerBits(int workerId)
    {
        var generator = new SnowflakeIdGenerator(Options.Create(new DataOptions { SnowflakeWorkerId = workerId }));

        WorkerId(generator.CreateId()).Should().Be(workerId);
    }

    private static long WorkerId(long id) => (id >> SEQUENCE_BITS) & WORKER_MASK;
}
