using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Outbox;

/// <summary>
/// Fails startup when delivery to the job queue is on and an inline outbox handler's work has no job subscription
/// — the most likely upgrade mistake, which would otherwise lose that work silently.
/// </summary>
internal sealed class OutboxSubscriptionsStartupCheck(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxDeliveryOptions> delivery) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!delivery.Value.DeliverToJobQueue)
        {
            return Task.CompletedTask;
        }

        using var scope = scopeFactory.CreateScope();
        var subscriptions = scope.ServiceProvider.GetService<OutboxSubscriptions>() ?? new OutboxSubscriptions([]);
        subscriptions.Validate(scope.ServiceProvider.GetServices<IOutboxIntegrationEventHandler>());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
