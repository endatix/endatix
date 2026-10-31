using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// How the host's stop ended, read from the message the host logs as the stop's last step.
/// </summary>
/// <remarks>
/// The host stops in the application's own <c>app.Run()</c> (see <see cref="StopOnceHost"/>), which throws a hosted
/// service's stop failure to the entry point, and <c>WebApplicationFactory</c> stops watching the entry point once the
/// host has started, so nothing else sees it. <c>Host.StopAsync</c> raises <c>ApplicationStopped</c> before it is
/// done; the last thing it does is log <c>Stopped</c> or, with the failure it then throws,
/// <c>StoppedWithException</c>, both at Debug. The tests log at Warning, so this provider alone hears that category at
/// Debug.
/// </remarks>
internal sealed class HostStopOutcome : ILoggerProvider, ILogger
{
    // Microsoft.Extensions.Hosting.Internal.Host and its LoggerEventIds.
    private const string HostCategory = "Microsoft.Extensions.Hosting.Internal.Host";
    private const int StoppedEventId = 4;
    private const int StoppedWithExceptionEventId = 5;

    private readonly TaskCompletionSource _outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the host has stopped, faulted with the stop's failure when it failed.</summary>
    public Task Stopped => _outcome.Task;

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<HostStopOutcome>();
        services.AddSingleton<ILoggerProvider>(provider => provider.GetRequiredService<HostStopOutcome>());
        services.Configure<LoggerFilterOptions>(options => options.AddFilter<HostStopOutcome>(HostCategory, LogLevel.Debug));
    }

    public ILogger CreateLogger(string categoryName) => categoryName == HostCategory ? this : NullLogger.Instance;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (eventId.Id == StoppedEventId)
        {
            _outcome.TrySetResult();
        }
        else if (eventId.Id == StoppedWithExceptionEventId)
        {
            _outcome.TrySetException(exception ?? new InvalidOperationException(formatter(state, exception)));
        }
    }

    public void Dispose()
    {
        // Nothing to release; the outcome outlives the container that disposes this.
    }
}
