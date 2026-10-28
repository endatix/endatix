using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test host's background work: its outbox relay and its jobs scheduler. Every test host starts with both off, so
/// what one test leaves behind cannot run while the next test resets the database. A test that needs them starts
/// them, and stops them before it ends.
/// </summary>
internal sealed class BackgroundWork(IServiceProvider services, OutboxRelaySwitch relay, JobsSchedulerSwitch scheduler)
{
    // Long enough for an import batch a test left running to finish; a stop that takes longer is a hang to report.
    private static readonly TimeSpan StopPatience = TimeSpan.FromSeconds(90);

    // Not the relay's own instance id, so a lease this delivery takes is never mistaken for one of the relay's.
    private const string DeliveringInstance = "integration-test-delivery";

    /// <summary>The background work of the host <paramref name="hostServices"/> belongs to.</summary>
    public static BackgroundWork Of(IServiceProvider hostServices) => hostServices.GetRequiredService<BackgroundWork>();

    /// <summary>How many times this host's outbox relay has asked whether to run.</summary>
    public int RelayChecks => relay.Checks;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        relay.TurnOn();
        return scheduler.RunAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the relay and puts the scheduler in standby, and returns once the relay batch and the jobs already
    /// running have finished. Stopping what is already stopped returns at once.
    /// </summary>
    /// <exception cref="TimeoutException">Background work was still running after <see cref="StopPatience"/>.</exception>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(StopPatience);
        try
        {
            await relay.TurnOffAsync().WaitAsync(patience.Token);
            await scheduler.StandbyAsync(patience.Token);
        }
        catch (OperationCanceledException timedOut) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The test host's background work was still running {StopPatience.TotalSeconds:0} s after it was told to stop.",
                timedOut);
        }
    }

    /// <summary>
    /// Hands every pending outbox message to its handlers once, in this call, as one relay tick after another would.
    /// Returns when no message is left to claim. A handler that throws fails the call.
    /// </summary>
    public async Task DeliverOutboxOnceAsync(CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<OutboxOptions>>().Value;
        int delivered;
        do
        {
            await using var scope = services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IOutboxClaimStore>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var batch = await store.ClaimBatchAsync(
                DeliveringInstance, options.Lease, options.BatchSize, cancellationToken);
            foreach (var message in batch)
            {
                await publisher.PublishAsync(message, cancellationToken);
                await store.MarkSentAsync(message, DeliveringInstance, cancellationToken);
            }

            delivered = batch.Count;
        }
        while (delivered > 0);
    }
}
