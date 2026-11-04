using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public sealed class WebHookHandlersStartupCheckTests
{
    [Fact]
    public async Task StartAsync_WebHookTypeWithoutAHandler_LogsAWarningForIt()
    {
        // Arrange
        var logger = new RecordingLogger();
        var services = new ServiceCollection()
            .AddScoped<IOutboxIntegrationEventHandler>(_ => new TypesHandler(WebHookEvents.EventTypes.Where(type => type != "submission.closed").ToArray()))
            .BuildServiceProvider();
        WebHookHandlersStartupCheck check = new(services.GetRequiredService<IServiceScopeFactory>(), logger);

        // Act
        await check.StartAsync(CancellationToken.None);

        // Assert
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("submission.closed");
    }

    [Fact]
    public async Task StartAsync_EveryWebHookTypeHandled_LogsNothing()
    {
        // Arrange
        var logger = new RecordingLogger();
        var services = new ServiceCollection()
            .AddScoped<IOutboxIntegrationEventHandler>(_ => new TypesHandler([.. WebHookEvents.EventTypes]))
            .BuildServiceProvider();
        WebHookHandlersStartupCheck check = new(services.GetRequiredService<IServiceScopeFactory>(), logger);

        // Act
        await check.StartAsync(CancellationToken.None);

        // Assert
        logger.Warnings.Should().BeEmpty();
    }

    private sealed class RecordingLogger : ILogger<WebHookHandlersStartupCheck>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private sealed class TypesHandler(IReadOnlyCollection<string> eventTypes) : IOutboxIntegrationEventHandler
    {
        public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
