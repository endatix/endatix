using System.Threading.Channels;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Runtime;

internal sealed class SinglePoolDispatchStrategy : IJobDispatchStrategy
{
    private const int QueuedItemsPerSlot = 25;

    private readonly Channel<JobDispatchItem> _channel;
    private readonly SemaphoreSlim _slots;

    public SinglePoolDispatchStrategy(IOptions<BackgroundJobsOptions> options)
    {
        var maxConcurrency = options.Value.MaxConcurrency;
        Guard.Against.NegativeOrZero(maxConcurrency);

        // Wait rather than DropWrite: under DropWrite, TryWrite reports a dropped item as written, and TryWrite
        // never waits in either mode. A rejected offer loses nothing, because the job stays eligible in its row.
        _channel = Channel.CreateBounded<JobDispatchItem>(new BoundedChannelOptions(maxConcurrency * QueuedItemsPerSlot)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });
        _slots = new SemaphoreSlim(maxConcurrency);
    }

    public int QueuedCount => _channel.Reader.Count;

    public bool TryOffer(JobDispatchItem item) => _channel.Writer.TryWrite(item);

    public async ValueTask<JobDispatchLease> AcquireAsync(CancellationToken ct)
    {
        await _slots.WaitAsync(ct);

        try
        {
            var item = await _channel.Reader.ReadAsync(ct);
            return new JobDispatchLease(item, () => _slots.Release());
        }
        catch
        {
            // No lease was handed out, so nothing else would give this slot back.
            _slots.Release();
            throw;
        }
    }
}
