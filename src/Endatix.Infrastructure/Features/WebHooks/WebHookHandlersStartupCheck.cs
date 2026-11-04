using Endatix.Infrastructure.Features.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.WebHooks;

/// <summary>
/// Logs a warning at startup for each webhook event type no outbox handler subscribes to: such an event would be
/// marked sent without a webhook.
/// </summary>
internal sealed partial class WebHookHandlersStartupCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<WebHookHandlersStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var handled = scope.ServiceProvider.GetServices<IOutboxIntegrationEventHandler>()
            .SelectMany(handler => handler.EventTypes)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var eventType in WebHookEvents.EventTypes.Where(eventType => !handled.Contains(eventType)))
        {
            LogUnhandled(eventType);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Webhook event type '{EventType}' has no outbox handler; its events are marked sent without a webhook.")]
    private partial void LogUnhandled(string eventType);
}
