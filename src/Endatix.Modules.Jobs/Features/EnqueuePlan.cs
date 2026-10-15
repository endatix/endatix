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

    /// <summary>
    /// The jobs the enqueue inserts, each once, in the order it inserts them: the jobs with a dedup key ordered by
    /// tenant, job type and key, then the jobs without one in request order.
    /// </summary>
    /// <remarks>
    /// Inserting a dedup key locks it in the unique index until the transaction ends. Two batches that inserted the
    /// same keys in opposite orders would each wait on a key the other holds, a deadlock the queue cannot tell from a
    /// failure; in one order for every batch, the second waits for the first and then sees its keys as a collision,
    /// which it resolves by returning the existing jobs. A job without a key takes no lock in that index, so its
    /// place does not matter, and it keeps the caller's order.
    /// </remarks>
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

        plan.NewJobs = [.. plan._newJobs.OfType<BackgroundJob>().Distinct().Order(InsertionOrder.Instance)];
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

/// <summary>
/// Keyed jobs first, by tenant, job type and dedup key compared ordinally; jobs without a key compare equal to each
/// other, so a stable sort keeps them in request order.
/// </summary>
internal sealed class InsertionOrder : IComparer<BackgroundJob>
{
    public static readonly InsertionOrder Instance = new();

    public int Compare(BackgroundJob? x, BackgroundJob? y) => (x, y) switch
    {
        ({ DedupKey: not null } keyedX, { DedupKey: not null } keyedY) => CompareIdentities(keyedX, keyedY),
        ({ DedupKey: not null }, _) => -1,
        (_, { DedupKey: not null }) => 1,
        _ => 0,
    };

    private static int CompareIdentities(BackgroundJob x, BackgroundJob y)
    {
        var byTenant = x.TenantId.CompareTo(y.TenantId);
        if (byTenant != 0)
        {
            return byTenant;
        }

        var byJobType = string.CompareOrdinal(x.JobType, y.JobType);
        return byJobType != 0 ? byJobType : string.CompareOrdinal(x.DedupKey, y.DedupKey);
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
