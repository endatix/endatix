using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.IntegrationTests;

/// <summary>Counts entry into any matched endpoint, including package-owned request delegates.</summary>
internal sealed class EndpointDispatches : ILoggerProvider
{
    internal const string CATEGORY = "Microsoft.AspNetCore.Routing.EndpointMiddleware";
    private readonly ILogger _logger = Substitute.For<ILogger>();
    internal int Count => _logger.ReceivedCalls().Count(call =>
        call.GetArguments().OfType<EventId>().Any(id => id.Name == "ExecutingEndpoint"));

    internal EndpointDispatches() => _logger.IsEnabled(LogLevel.Information).Returns(true);

    public ILogger CreateLogger(string categoryName) => categoryName == CATEGORY ? _logger : NullLogger.Instance;
    public void Dispose() { }
}
