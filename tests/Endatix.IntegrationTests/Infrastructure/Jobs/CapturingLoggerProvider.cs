using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>Keeps every formatted log message a node writes at <paramref name="minimum"/> or above, Warning unless given.</summary>
internal sealed class CapturingLoggerProvider(LogLevel minimum = LogLevel.Warning) : ILoggerProvider
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    public IReadOnlyCollection<(LogLevel Level, string Message)> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries, minimum);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<(LogLevel, string)> entries, LogLevel minimum) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, exception);
                entries.Enqueue((logLevel, exception is null ? message : $"{message} {exception}"));
            }
        }
    }
}
