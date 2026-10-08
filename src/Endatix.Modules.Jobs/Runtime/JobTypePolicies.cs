using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The policy each job type runs under on this host. Attempts, retention and the scheduler's thread pool and
/// per-type caps all read it here, so they cannot disagree about a job type's settings.
/// </summary>
internal sealed class JobTypePolicies(IOptions<BackgroundJobsOptions> options, JobTypeDefaults defaults)
{
    public BackgroundJobTypePolicy For(string jobType) => options.Value.ResolvePolicy(jobType, defaults.For(jobType));
}
