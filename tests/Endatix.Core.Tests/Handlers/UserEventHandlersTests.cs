using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Handlers;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.Handlers;

public class UserEventHandlersTests
{
    private static readonly User TestUser = new(1, SampleData.TENANT_ID, "test@example.com", "test@example.com", true);

    private static ILogger<T> TraceLogger<T>()
    {
        var logger = Substitute.For<ILogger<T>>();
        logger.IsEnabled(LogLevel.Trace).Returns(true);
        return logger;
    }

    private static void ShouldHaveLoggedTrace<T>(ILogger<T> logger, string expectedMessage) =>
        logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => (Level: (LogLevel)call.GetArguments()[0]!, Message: call.GetArguments()[2]?.ToString()))
            .Should().ContainSingle(entry => entry.Level == LogLevel.Trace && entry.Message!.StartsWith(expectedMessage));

    [Fact]
    public async Task Handle_UserLoggedIn_LogsTrace()
    {
        // Arrange
        var logger = TraceLogger<UserLoggedInEventHandler>();
        var sut = new UserLoggedInEventHandler(logger);

        // Act
        await sut.Handle(new UserLoggedInEvent(TestUser), CancellationToken.None);

        // Assert
        ShouldHaveLoggedTrace(logger, "Handling User LoggedIn event for");
    }

    [Fact]
    public async Task Handle_UserLoggedOut_LogsTrace()
    {
        // Arrange
        var logger = TraceLogger<UserLoggedOutEventHandler>();
        var sut = new UserLoggedOutEventHandler(logger);

        // Act
        await sut.Handle(new UserLoggedOutEvent(TestUser), CancellationToken.None);

        // Assert
        ShouldHaveLoggedTrace(logger, "Handling User LoggedOut event for");
    }

    [Fact]
    public async Task Handle_UserRegistered_LogsTrace()
    {
        // Arrange
        var logger = TraceLogger<UserRegisteredEventHandler>();
        var sut = new UserRegisteredEventHandler(logger);

        // Act
        await sut.Handle(new UserRegisteredEvent(TestUser), CancellationToken.None);

        // Assert
        ShouldHaveLoggedTrace(logger, "Handling User Registered event for");
    }
}
