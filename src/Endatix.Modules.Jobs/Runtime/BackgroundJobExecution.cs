using Quartz;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The only scheduler job class. One durable scheduler job per job type points at it, and each trigger carries
/// only the id of the job row it fires.
/// </summary>
/// <remarks>
/// Handlers never see this class or any scheduler type: everything scheduler-specific stays here, so replacing
/// the scheduler rewrites this class and not the handlers.
/// </remarks>
internal sealed class BackgroundJobExecution : IJob
{
    /// <summary>The trigger data key that holds the job row's id.</summary>
    public const string JobIdKey = "jobId";

    public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) => default;
}
