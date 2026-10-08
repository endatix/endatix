using Endatix.Infrastructure.Features.BackgroundJobs;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The defaults job types' owners declared when they registered their handlers, by job type.
/// </summary>
/// <remarks>
/// Built from the declared records alone, never from the handlers, so options validation can read it without
/// building a handler. Job types are compared ordinally, as the handler registry compares them.
/// </remarks>
internal sealed class JobTypeDefaults
{
    private readonly Dictionary<string, BackgroundJobTypeDefaults> _byJobType = new(StringComparer.Ordinal);

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> naming the job type when two records declare the same one,
    /// which would leave its settings ambiguous.
    /// </summary>
    public JobTypeDefaults(IEnumerable<BackgroundJobTypeDefaults> declared)
    {
        foreach (var defaults in declared)
        {
            Add(defaults);
        }
    }

    /// <summary>The job types that declared defaults.</summary>
    public IReadOnlyCollection<string> JobTypes => _byJobType.Keys;

    /// <summary>The defaults <paramref name="jobType"/> declared, or <see langword="null"/> when it declared none.</summary>
    public BackgroundJobTypeDefaults? For(string jobType) => _byJobType.GetValueOrDefault(jobType);

    private void Add(BackgroundJobTypeDefaults defaults)
    {
        if (!_byJobType.TryAdd(defaults.JobType, defaults))
        {
            throw new InvalidOperationException(
                $"More than one set of defaults is registered for the background job type '{defaults.JobType}'.");
        }
    }
}
