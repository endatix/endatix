using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Features;

namespace Endatix.Modules.Jobs.Tests.Features;

public sealed class EnqueuePlanTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Of_KeyedAndUnkeyedRequests_InsertsKeyedJobsByIdentityThenTheRestInRequestOrder()
    {
        // Arrange
        BackgroundJobRequest[] requests =
        [
            Unkeyed("first unkeyed"),
            Keyed(2, "B", "k1"),
            Keyed(1, "B", "k2"),
            Unkeyed("second unkeyed"),
            Keyed(1, "A", "k9"),
            Keyed(1, "B", "k1"),
        ];

        // Act
        var plan = EnqueuePlan.Of(requests, new Dictionary<DedupIdentity, long>(), Create);

        // Assert
        plan.NewJobs.Select(job => (job.TenantId, job.JobType, job.DedupKey ?? job.PayloadJson)).Should().Equal(
            (1L, "A", "k9"),
            (1L, "B", "k1"),
            (1L, "B", "k2"),
            (2L, "B", "k1"),
            (1L, "B", "first unkeyed"),
            (1L, "B", "second unkeyed"));
    }

    [Fact]
    public void Of_SameKeysInOppositeOrder_InsertsThemInTheSameOrder()
    {
        // Arrange — two batches racing on the same keys must lock them in one order, or they can deadlock.
        BackgroundJobRequest[] forward = [Keyed(1, "T", "a"), Keyed(1, "T", "b"), Keyed(1, "T", "c")];
        BackgroundJobRequest[] backward = [.. forward.Reverse()];

        // Act
        var first = EnqueuePlan.Of(forward, new Dictionary<DedupIdentity, long>(), Create);
        var second = EnqueuePlan.Of(backward, new Dictionary<DedupIdentity, long>(), Create);

        // Assert
        first.NewJobs.Select(job => job.DedupKey).Should().Equal("a", "b", "c");
        second.NewJobs.Select(job => job.DedupKey).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Of_KeysDifferingOnlyInCase_OrdersThemOrdinally()
    {
        // Arrange — the dedup columns compare ordinally on both providers.
        BackgroundJobRequest[] requests = [Keyed(1, "T", "b"), Keyed(1, "T", "B")];

        // Act
        var plan = EnqueuePlan.Of(requests, new Dictionary<DedupIdentity, long>(), Create);

        // Assert
        plan.NewJobs.Select(job => job.DedupKey).Should().Equal("B", "b");
    }

    private static BackgroundJobRequest Keyed(long tenantId, string jobType, string dedupKey) =>
        new(jobType, "{}", tenantId, DedupKey: dedupKey);

    // Told apart by payload, since a job without a key has nothing else of its own.
    private static BackgroundJobRequest Unkeyed(string payload) => new("B", payload, TenantId: 1);

    private static BackgroundJob Create(BackgroundJobRequest request) => BackgroundJob.FromRequest(request, Now, traceId: null);
}
