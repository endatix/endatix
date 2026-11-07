using Quartz;
using Quartz.Extensibility;
using Quartz.Impl;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The scheduler's job store, refusing to complete a firing once the store has shut down, so that a firing the
/// scheduler stopped waiting for at shutdown is left for recovery rather than settled.
/// </summary>
/// <remarks>
/// <para>
/// A host gives running jobs <c>ShutdownWaitSeconds</c> to finish; a job still running then is told to stop only
/// once the scheduler has shut down (<see cref="JobsShutdownSignal"/>), records nothing, and relies on its
/// fired-trigger row staying behind, so that the next node to check in recovers it and runs it again. Quartz up to
/// 4.2 refused a completion after the store had shut down. Since 4.3 a firing that may run beside itself completes
/// without the trigger lock, on a path that never asks whether the store has shut down
/// (<see href="https://github.com/quartznet/quartznet/issues/3863">quartznet#3863</see>), so the late completion
/// deleted the row and the job was never run again. This puts the refusal back.
/// </para>
/// <para>
/// Everything else goes to the store unchanged, acquisition included: the store's own round, not the decorator's
/// default, which acquires and fires apart.
/// </para>
/// </remarks>
internal sealed class ShutdownGuardedJobStore(IJobStore store) : DelegatingJobStore(store)
{
    private volatile bool _shutDown;

    /// <inheritdoc />
    public override ValueTask Shutdown(CancellationToken cancellationToken = default)
    {
        _shutDown = true;
        return base.Shutdown(cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask<TriggerAcquisitionResult> AcquireNextTriggersAndFireDue(
        TriggerAcquisitionRequest request,
        CancellationToken cancellationToken = default) =>
        InnerJobStore.AcquireNextTriggersAndFireDue(request, cancellationToken);

    /// <inheritdoc />
    public override ValueTask FiringComplete(TriggeredJobCompleteContext context, CancellationToken cancellationToken = default)
    {
        RefuseAfterShutdown();
        return base.FiringComplete(context, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask TriggeredJobComplete(
        IOperableTrigger trigger,
        IJobDetail jobDetail,
        SchedulerInstruction triggerInstructionCode,
        CancellationToken cancellationToken = default)
    {
        RefuseAfterShutdown();
        return base.TriggeredJobComplete(trigger, jobDetail, triggerInstructionCode, cancellationToken);
    }

    // As Quartz's store refused it up to 4.2, and still does on its locked path: an exception the job's run reports.
    private void RefuseAfterShutdown()
    {
        if (_shutDown)
        {
            throw new InvalidOperationException(
                "The job store has shut down, so the firing is left for recovery on the next node to check in.");
        }
    }
}
