using Endatix.Core.Abstractions.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Builds the context a handler is invoked with from the row the claim returned.
/// </summary>
/// <remarks>
/// A seam rather than a projection inside the executor: a host that carries more than the row's own fields into a
/// handler replaces this, and nothing about claiming or recording an attempt changes with it.
/// </remarks>
internal interface IJobExecutionContextResolver
{
    BackgroundJobContext Resolve(ClaimedJob job);
}
