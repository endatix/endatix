using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public class SinglePoolDispatchStrategyTests
{
    private const int MaxConcurrency = 4;

    private long _lastJobId;

    [Fact]
    public void TryOffer_ChannelFull_ReturnsFalseWithoutBlocking()
    {
        // Arrange — four slots queue a hundred items.
        var strategy = CreateStrategy();

        // Act
        var accepted = Enumerable.Range(0, 100).Select(_ => strategy.TryOffer(NextItem())).ToList();
        var surplus = strategy.TryOffer(NextItem());

        // Assert — TryOffer returns its answer rather than a task, so every call here finished before the next began.
        accepted.Should().HaveCount(100).And.OnlyContain(offered => offered);
        surplus.Should().BeFalse();
        strategy.QueuedCount.Should().Be(100);
    }

    [Fact]
    public async Task AcquireAsync_AllSlotsHeld_WaitsForRelease()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var strategy = CreateStrategy();
        var leases = HoldLeases(strategy, MaxConcurrency);
        var queued = NextItem();
        strategy.TryOffer(queued);

        // Act
        var acquisition = strategy.AcquireAsync(cancellationToken).AsTask();
        var completedWhileSlotsHeld = acquisition.IsCompleted;
        leases[0].Dispose();
        var lease = await acquisition;

        // Assert
        completedWhileSlotsHeld.Should().BeFalse();
        lease.Item.Should().Be(queued);
    }

    [Fact]
    public async Task AcquireAsync_OfferedItems_ReturnsInOfferOrder()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var strategy = CreateStrategy();
        var first = new JobDispatchItem(1, "A");
        var second = new JobDispatchItem(2, "B");
        strategy.TryOffer(first);
        strategy.TryOffer(second);

        // Act
        using var firstLease = await strategy.AcquireAsync(cancellationToken);
        using var secondLease = await strategy.AcquireAsync(cancellationToken);

        // Assert
        firstLease.Item.Should().Be(first);
        secondLease.Item.Should().Be(second);
    }

    [Fact]
    public void QueuedCount_AfterAcquire_ReflectsRemaining()
    {
        // Arrange
        var strategy = CreateStrategy();
        Offer(strategy, 5);

        // Act
        AcquireQueued(strategy, 2);

        // Assert
        strategy.QueuedCount.Should().Be(3);
    }

    [Theory]
    // Every slot is held, so the cancelled call is still waiting for a slot.
    [InlineData(MaxConcurrency)]
    // A slot is free and nothing is queued, so the cancelled call has taken the slot and is waiting for an item.
    [InlineData(MaxConcurrency - 1)]
    public async Task AcquireAsync_Cancelled_ReleasesSlot(int heldLeases)
    {
        // Arrange
        var strategy = CreateStrategy();
        var leases = HoldLeases(strategy, heldLeases);
        using var cancellation = new CancellationTokenSource();
        var acquisition = strategy.AcquireAsync(cancellation.Token).AsTask();
        var completedBeforeCancel = acquisition.IsCompleted;

        // Act
        await cancellation.CancelAsync();
        var act = () => acquisition;

        // Assert — once the held leases are disposed, every slot can be held again, and no more.
        completedBeforeCancel.Should().BeFalse();
        await act.Should().ThrowAsync<OperationCanceledException>();
        DisposeAll(leases);
        HoldLeases(strategy, MaxConcurrency);
        strategy.TryOffer(NextItem());
        var beyondCapacity = strategy.AcquireAsync(TestContext.Current.CancellationToken);
        beyondCapacity.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_ReleasesOnce()
    {
        // Arrange
        var strategy = CreateStrategy();
        var lease = HoldLeases(strategy, 1)[0];

        // Act
        lease.Dispose();
        lease.Dispose();

        // Assert — the one slot given back lets four leases be held again, and nothing more.
        HoldLeases(strategy, MaxConcurrency);
        strategy.TryOffer(NextItem());
        var fifth = strategy.AcquireAsync(TestContext.Current.CancellationToken);
        fifth.IsCompleted.Should().BeFalse();
    }

    private static SinglePoolDispatchStrategy CreateStrategy() =>
        new(Options.Create(new BackgroundJobsOptions { MaxConcurrency = MaxConcurrency }));

    private JobDispatchItem NextItem() => new(++_lastJobId, "Test.Job");

    private void Offer(SinglePoolDispatchStrategy strategy, int count)
    {
        for (var offered = 0; offered < count; offered++)
        {
            strategy.TryOffer(NextItem());
        }
    }

    private List<JobDispatchLease> HoldLeases(SinglePoolDispatchStrategy strategy, int count)
    {
        Offer(strategy, count);
        return AcquireQueued(strategy, count);
    }

    // With a slot free and an item queued an acquisition has nothing to wait for, so one that does not complete at
    // once fails here instead of hanging the test.
    private static List<JobDispatchLease> AcquireQueued(SinglePoolDispatchStrategy strategy, int count)
    {
        List<JobDispatchLease> leases = [];
        for (var acquired = 0; acquired < count; acquired++)
        {
            var acquisition = strategy.AcquireAsync(TestContext.Current.CancellationToken);
            acquisition.IsCompletedSuccessfully.Should().BeTrue("slot {0} is free and an item is queued", acquired + 1);
            leases.Add(acquisition.Result);
        }

        return leases;
    }

    private static void DisposeAll(IEnumerable<JobDispatchLease> leases)
    {
        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }
}
