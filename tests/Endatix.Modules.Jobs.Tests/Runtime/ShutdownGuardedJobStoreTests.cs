using Endatix.Modules.Jobs.Runtime;
using Quartz;
using Quartz.Extensibility;


namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class ShutdownGuardedJobStoreTests
{
    [Fact]
    public async Task FiringComplete_BeforeShutdown_CompletesInTheStore()
    {
        // Arrange
        var inner = Substitute.For<IJobStore>();
        var store = new ShutdownGuardedJobStore(inner);
        var completion = Completion();

        // Act
        await store.FiringComplete(completion, TestContext.Current.CancellationToken);

        // Assert
        await inner.Received(1).FiringComplete(completion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FiringComplete_AfterShutdown_IsRefusedAndNeverReachesTheStore()
    {
        // Arrange — a job the scheduler stopped waiting for ends after the store shut down. Its fired-trigger row has
        // to stay, so that recovery runs the job again.
        var inner = Substitute.For<IJobStore>();
        var store = new ShutdownGuardedJobStore(inner);
        await store.Shutdown(TestContext.Current.CancellationToken);

        // Act
        var complete = async () => await store.FiringComplete(Completion(), TestContext.Current.CancellationToken);

        // Assert
        await complete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*shut down*");
        await inner.DidNotReceive().FiringComplete(Arg.Any<TriggeredJobCompleteContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireNextTriggersAndFireDue_Always_IsTheInnerStoresOwn()
    {
        // Arrange — the decorator's default acquires and fires apart; the store's own round is what the module ran on.
        var inner = Substitute.For<IJobStore>();
        var store = new ShutdownGuardedJobStore(inner);
        var request = new TriggerAcquisitionRequest { NoLaterThan = DateTimeOffset.UtcNow };

        // Act
        await store.AcquireNextTriggersAndFireDue(request, TestContext.Current.CancellationToken);

        // Assert
        await inner.Received(1).AcquireNextTriggersAndFireDue(request, Arg.Any<CancellationToken>());
    }

    private static TriggeredJobCompleteContext Completion() => new()
    {
        Trigger = Substitute.For<IOperableTrigger>(),
        JobDetail = Substitute.For<IJobDetail>(),
        Instruction = SchedulerInstruction.DeleteTrigger,
    };
}
