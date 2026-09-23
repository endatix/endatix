using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <inheritdoc cref="IJobExecutionContextResolver" />
internal sealed class JobRowExecutionContextResolver : IJobExecutionContextResolver
{
    /// <summary>
    /// The tenant and the attempt are the claimed row's own, so a handler scopes its queries to the job's tenant
    /// rather than to whichever tenant the process happens to be serving.
    /// </summary>
    public BackgroundJobContext Resolve(ClaimedJob job) =>
        new(job.Id, job.JobType, job.TenantId, job.PayloadJson, job.AttemptCount);
}
