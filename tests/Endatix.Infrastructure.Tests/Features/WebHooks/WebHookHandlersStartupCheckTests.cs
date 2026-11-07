using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Infrastructure.Tests.Features.ReCaptcha;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public sealed class WebHookHandlersStartupCheckTests
{
    [Fact]
    public async Task StartAsync_WebHookTypeWithoutAHandler_LogsAWarningForIt()
    {
        // Arrange
        var logger = new RecordingLogger<WebHookHandlersStartupCheck>();
        var services = new ServiceCollection()
            .AddScoped<IOutboxIntegrationEventHandler>(_ => new TypesHandler(WebHookEvents.EventTypes.Where(type => type != "submission.closed").ToArray()))
            .BuildServiceProvider();
        WebHookHandlersStartupCheck check = new(services.GetRequiredService<IServiceScopeFactory>(), logger);

        // Act
        await check.StartAsync(CancellationToken.None);

        // Assert
        logger.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task StartAsync_EveryWebHookTypeHandled_LogsNothing()
    {
        // Arrange
        var logger = new RecordingLogger<WebHookHandlersStartupCheck>();
        var services = new ServiceCollection()
            .AddScoped<IOutboxIntegrationEventHandler>(_ => new TypesHandler([.. WebHookEvents.EventTypes]))
            .BuildServiceProvider();
        WebHookHandlersStartupCheck check = new(services.GetRequiredService<IServiceScopeFactory>(), logger);

        // Act
        await check.StartAsync(CancellationToken.None);

        // Assert
        logger.Entries.Should().BeEmpty();
    }

    private sealed class TypesHandler(IReadOnlyCollection<string> eventTypes) : IOutboxIntegrationEventHandler
    {
        public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
