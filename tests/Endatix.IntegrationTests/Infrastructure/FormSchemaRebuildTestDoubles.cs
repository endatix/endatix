using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.IntegrationTests;

/// <summary>Passes every call to the repository it wraps; a test double overrides the calls it watches.</summary>
internal abstract class ForwardingSchemas(IFormSchemaRepository inner) : IFormSchemaRepository
{
    public Task<FormSchema?> GetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        inner.GetByFormIdAsync(tenantId, formId, cancellationToken);

    public virtual Task<FormSchema?> LockAndGetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        inner.LockAndGetByFormIdAsync(tenantId, formId, cancellationToken);

    public virtual Task SaveAsync(FormSchema schema, CancellationToken cancellationToken) =>
        inner.SaveAsync(schema, cancellationToken);

    public Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        inner.DeleteByFormIdAsync(tenantId, formId, cancellationToken);
}

/// <summary>Lets every party go on once all have arrived, or once the timeout passes.</summary>
internal sealed class Rendezvous(int parties)
{
    // Long enough for every party to read the schema before any saves, when nothing keeps them apart.
    private static readonly TimeSpan AllArriveTimeout = TimeSpan.FromSeconds(2);

    private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public async Task ArriveAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _arrived) == parties)
        {
            _allArrived.TrySetResult();
        }

        await Task.WhenAny(_allArrived.Task, Task.Delay(AllArriveTimeout, cancellationToken));
    }
}

// Holds each rebuild after it has read the schema, so rebuilds that are not kept apart both read it before
// either saves. A rebuild that has to wait for the lock arrives only after the other one committed.
internal sealed class WaitAfterLockedRead(IFormSchemaRepository inner, Rendezvous rendezvous) : ForwardingSchemas(inner)
{
    public override async Task<FormSchema?> LockAndGetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var schema = await base.LockAndGetByFormIdAsync(tenantId, formId, cancellationToken);
        await rendezvous.ArriveAsync(cancellationToken);
        return schema;
    }
}

// Runs some work once, before the first rebuild takes the lock.
internal sealed class BeforeLockedRead(IFormSchemaRepository inner, Func<Task> work) : ForwardingSchemas(inner)
{
    private Func<Task>? _pendingWork = work;

    public override async Task<FormSchema?> LockAndGetByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        var pendingWork = Interlocked.Exchange(ref _pendingWork, null);
        if (pendingWork is not null)
        {
            await pendingWork();
        }

        return await base.LockAndGetByFormIdAsync(tenantId, formId, cancellationToken);
    }
}

internal sealed class CountingSaves(IFormSchemaRepository inner, RebuildCounter counter) : ForwardingSchemas(inner)
{
    public override Task SaveAsync(FormSchema schema, CancellationToken cancellationToken)
    {
        counter.CountSave();
        return base.SaveAsync(schema, cancellationToken);
    }
}

/// <summary>Counts the schema saves and flattened-row deletes of several rebuilds.</summary>
internal sealed class RebuildCounter
{
    private int _saves;

    public RebuildCounter()
    {
        FlattenedRows.DeleteByFormIdAsync(default, default, default).ReturnsForAnyArgs(0);
    }

    public IFlattenedSubmissionRepository FlattenedRows { get; } = Substitute.For<IFlattenedSubmissionRepository>();

    public int Saves => Volatile.Read(ref _saves);

    public int Deletes => FlattenedRows.ReceivedCalls()
        .Count(call => call.GetMethodInfo().Name == nameof(IFlattenedSubmissionRepository.DeleteByFormIdAsync));

    public void CountSave() => Interlocked.Increment(ref _saves);
}

/// <summary>Waits a fraction of a second for a lock, so a test sees the timeout without waiting it out.</summary>
internal sealed class ShortWaitLock(ITransactionLock inner) : ITransactionLock
{
    public Task AcquireAsync(DatabaseFacade database, TransactionLockRequest request, CancellationToken cancellationToken) =>
        inner.AcquireAsync(database, request with { Timeout = TimeSpan.FromMilliseconds(300) }, cancellationToken);
}
