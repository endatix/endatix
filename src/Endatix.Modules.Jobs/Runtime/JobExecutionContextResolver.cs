using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Builds the context a handler runs with from the row its claim returned.
/// </summary>
/// <remarks>
/// One method, so how a job's scope reaches its handler changes here and nothing about claiming or recording an
/// attempt.
/// </remarks>
internal interface IJobExecutionContextResolver
{
    BackgroundJobContext Resolve(ClaimedJob job);
}

/// <inheritdoc cref="IJobExecutionContextResolver" />
internal sealed class JobRowExecutionContextResolver : IJobExecutionContextResolver
{
    /// <summary>
    /// The scope is the claimed row's own and is only passed to the handler, never made ambient: outside a
    /// request the ambient tenant filter is permissive, and a handler must scope its queries explicitly. A system
    /// job runs with no tenant, as the outbox relay does inline.
    /// </summary>
    public BackgroundJobContext Resolve(ClaimedJob job) =>
        new(job.Id, job.JobType, JobScopeStorage.FromStorage(job.TenantId), job.PayloadJson, job.AttemptCount);
}
