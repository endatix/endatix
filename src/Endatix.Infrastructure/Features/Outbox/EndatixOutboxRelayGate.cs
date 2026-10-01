using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Keeps the relay from claiming messages it cannot deliver: with delivery to the job queue on and no job queue
/// registered — the Jobs module off — a claimed message could only be dropped or retried into a dead letter.
/// Otherwise it defers to the relay's usual switch.
/// </summary>
internal sealed class EndatixOutboxRelayGate(
    IOptions<OutboxDeliveryOptions> delivery,
    IServiceProviderIsService registeredServices,
    OpenFeatureOutboxRelayGate inner,
    EndatixOutboxRelayGate.PauseState pauseState,
    ILogger<EndatixOutboxRelayGate> logger) : IOutboxRelayGate
{
    public const string PausedMessage =
        "The outbox relay is paused: Endatix:Outbox:DeliverToJobQueue is on but the Jobs module is not registered.";

    public Task<bool> IsRelayEnabledAsync(CancellationToken cancellationToken)
    {
        if (delivery.Value.DeliverToJobQueue && !registeredServices.IsService(typeof(IBackgroundJobQueue)))
        {
            if (pauseState.EnterPaused())
            {
                logger.LogError(PausedMessage);
            }

            return Task.FromResult(false);
        }

        pauseState.Leave();
        return inner.IsRelayEnabledAsync(cancellationToken);
    }

    /// <summary>
    /// Remembers across ticks whether the relay is paused, so the error is logged once per pause rather than
    /// once per tick.
    /// </summary>
    internal sealed class PauseState
    {
        private int _paused;

        /// <summary>Returns <see langword="true"/> only for the tick that enters the pause.</summary>
        public bool EnterPaused() => Interlocked.Exchange(ref _paused, 1) == 0;

        public void Leave() => Interlocked.Exchange(ref _paused, 0);
    }
}
