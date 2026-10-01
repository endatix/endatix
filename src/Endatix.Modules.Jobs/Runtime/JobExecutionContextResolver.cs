using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Builds the context a handler runs with from the row its claim returned.
/// </summary>
/// <remarks>
/// One method, so a later job type that runs outside any tenant changes this and nothing about claiming or
/// recording an attempt.
/// </remarks>
internal interface IJobExecutionContextResolver
{
    BackgroundJobContext Resolve(ClaimedJob job);
}

/// <inheritdoc cref="IJobExecutionContextResolver" />
internal sealed class JobRowExecutionContextResolver : IJobExecutionContextResolver
{
    /// <summary>
    /// The tenant is the claimed row's own and is only passed to the handler, never made ambient: outside a
    /// request the ambient tenant filter is permissive, and a handler must scope its queries explicitly.
    /// </summary>
    public BackgroundJobContext Resolve(ClaimedJob job) =>
        new(job.Id, job.JobType, job.TenantId, job.PayloadJson, job.AttemptCount);
}
