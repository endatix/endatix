using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;

namespace Endatix.Modules.Jobs.Features;

/// <summary>
/// Which job each request of one enqueue maps to: one job per unit of work. A request whose dedup key is already in
/// the table gets that job's id, requests that repeat a key within the batch share one new job, and every other
/// request gets a job of its own.
/// </summary>
internal sealed class EnqueuePlan
{
    private readonly long?[] _existingIds;
    private readonly BackgroundJob?[] _newJobs;
    private readonly Dictionary<DedupIdentity, BackgroundJob> _newJobsByKey = [];
    private readonly Func<BackgroundJobRequest, BackgroundJob> _createJob;

    private EnqueuePlan(int requestCount, Func<BackgroundJobRequest, BackgroundJob> createJob)
    {
        _existingIds = new long?[requestCount];
        _newJobs = new BackgroundJob?[requestCount];
        _createJob = createJob;
        NewJobs = [];
    }

    /// <summary>The jobs the enqueue inserts, each once, in request order.</summary>
    public IReadOnlyList<BackgroundJob> NewJobs { get; private set; }

    /// <param name="requests">The requests, in the order the caller supplied them.</param>
    /// <param name="existing">The id of the job already in the table for each dedup identity that has one.</param>
    /// <param name="createJob">Creates the job a request asks for, once per job the enqueue inserts.</param>
    public static EnqueuePlan Of(
        IReadOnlyList<BackgroundJobRequest> requests,
        IReadOnlyDictionary<DedupIdentity, long> existing,
        Func<BackgroundJobRequest, BackgroundJob> createJob)
    {
        var plan = new EnqueuePlan(requests.Count, createJob);
        for (var index = 0; index < requests.Count; index++)
        {
            plan.Assign(index, requests[index], existing);
        }

        plan.NewJobs = [.. plan._newJobs.OfType<BackgroundJob>().Distinct()];
        return plan;
    }

    /// <summary>
    /// The job id of each request, in request order; read once the new jobs are saved and have their ids.
    /// </summary>
    public long[] Ids() => [.. _existingIds.Select((existingId, index) => existingId ?? _newJobs[index]!.Id)];

    private void Assign(int index, BackgroundJobRequest request, IReadOnlyDictionary<DedupIdentity, long> existing)
    {
        var identity = DedupIdentity.Of(request);
        if (identity is { } key && existing.TryGetValue(key, out var existingId))
        {
            _existingIds[index] = existingId;
            return;
        }

        _newJobs[index] = NewJobFor(request, identity);
    }

    private BackgroundJob NewJobFor(BackgroundJobRequest request, DedupIdentity? identity)
    {
        if (identity is not { } key)
        {
            return _createJob(request);
        }

        if (!_newJobsByKey.TryGetValue(key, out var job))
        {
            job = _createJob(request);
            _newJobsByKey[key] = job;
        }

        return job;
    }
}

/// <summary>What a dedup key is unique within.</summary>
internal readonly record struct DedupIdentity(long TenantId, string JobType, string DedupKey)
{
    public static DedupIdentity? Of(BackgroundJobRequest request) =>
        string.IsNullOrWhiteSpace(request.DedupKey)
            ? null
            : new DedupIdentity(request.TenantId, request.JobType, request.DedupKey);
}
