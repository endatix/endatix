using Microsoft.Extensions.Logging;

namespace Endatix.Api.Tests.Setup;

/// <summary>Records the warnings a host logs, through a substitute logger provider the test registers.</summary>
internal sealed class WarningLog
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public WarningLog()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        Provider = Substitute.For<ILoggerProvider>();
        Provider.CreateLogger(Arg.Any<string>()).Returns(_logger);
    }

    public ILoggerProvider Provider { get; }

    public IReadOnlyList<string> Warnings =>
        _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log)
                && Equals(call.GetArguments()[0], LogLevel.Warning))
            .Select(call => call.GetArguments()[2]?.ToString() ?? string.Empty)
            .ToList();
}
