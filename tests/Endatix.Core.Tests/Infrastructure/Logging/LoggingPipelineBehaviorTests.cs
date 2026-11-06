using System.Security.Claims;
using Endatix.Core.Infrastructure.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.Infrastructure.Logging;

public class LoggingPipelineBehaviorTests
{
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly LoggingPipelineBehavior<TestRequest, TestResponse> _sut;

    public LoggingPipelineBehaviorTests()
    {
        _logger = Substitute.For<ILogger>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(_logger);
        _sut = new LoggingPipelineBehavior<TestRequest, TestResponse>(_loggerFactory);
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        TestRequest? request = null;
        var next = new RequestHandlerDelegate<TestResponse>(() => Task.FromResult(new TestResponse()));

        // Act
        var act = () => _sut.Handle(request!, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithMessage(ErrorMessages.GetErrorMessage("request", ErrorType.Null));
    }

    [Fact]
    public void Constructor_AnyClosedGeneric_UsesSharedLogCategory()
    {
        // Act
        _ = new LoggingPipelineBehavior<TestRequestWithPrincipal, TestResponse>(_loggerFactory);

        // Assert
        _loggerFactory.Received().CreateLogger(LoggingPipelineBehavior<TestRequest, TestResponse>.LogCategory);
    }

    [Fact]
    public async Task Handle_LoggingDisabled_LogsNothing()
    {
        // Arrange
        var request = new TestRequest { Id = 1, Name = "Test" };
        var response = new TestResponse { Result = "Success" };
        var next = new RequestHandlerDelegate<TestResponse>(() => Task.FromResult(response));
        _logger.IsEnabled(LogLevel.Information).Returns(false);

        // Act
        var result = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(response);

        var logCalls = _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "Log")
            .Select(call => call.GetArguments()[2]?.ToString())
            .ToList();

        logCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LoggingEnabled_LogsClaimsPrincipalSummary()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);
        var sut = new LoggingPipelineBehavior<TestRequestWithPrincipal, TestResponse>(loggerFactory);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-123")
        ], authenticationType: "Keycloak"));
        var request = new TestRequestWithPrincipal { Id = 1, SubmitterPrincipal = principal };
        var response = new TestResponse { Result = "Success" };
        var next = new RequestHandlerDelegate<TestResponse>(() => Task.FromResult(response));
        logger.IsEnabled(LogLevel.Information).Returns(true);

        // Act
        await sut.Handle(request, next, CancellationToken.None);

        // Assert
        var logCalls = logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "Log")
            .Select(call => call.GetArguments()[2]?.ToString())
            .ToList();

        logCalls.Should().Contain("Property SubmitterPrincipal : ClaimsPrincipal(authenticated=True, userId=user-123)");
    }

    [Fact]
    public async Task Handle_LoggingEnabled_LogsRequestDetailsAndResponse()
    {
        // Arrange
        var request = new TestRequest { Id = 1, Name = "Test", Password = "secret" };
        var response = new TestResponse { Result = "Success" };
        var next = new RequestHandlerDelegate<TestResponse>(() => Task.FromResult(response));
        _logger.IsEnabled(LogLevel.Information).Returns(true);

        // Act
        var result = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(response);

        var logCalls = _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "Log")
            .Select(call => call.GetArguments()[2]?.ToString())
            .ToList();
        
        logCalls.Should().HaveCount(5);
        logCalls.Should().SatisfyRespectively(
            s => s.Should().Be("Handling TestRequest"),
            s => s.Should().Be("Property Id : 1"),
            s => s.Should().Be("Property Name : T***"),
            s =>
            {
                s.Should().StartWith("Property Password : ");
                var mask = s.Replace("Property Password : ", "");
                mask.Should().MatchRegex(@"^\*+$");
                mask.Length.Should().BeInRange(PiiRedactor.SECRET_LENGTH_MIN_LENGTH, PiiRedactor.SECRET_LENGTH_MAX_LENGTH - 1);
            },
            s => s.Should().StartWith($"Handled TestRequest with {response} in")
        );
    }

    private class TestRequestWithPrincipal : IRequest<TestResponse>
    {
        public int Id { get; set; }
        public ClaimsPrincipal? SubmitterPrincipal { get; set; }
    }

    private class TestRequest : IRequest<TestResponse>
    {
        public int Id { get; set; }

        [Sensitive(SensitivityType.Name)]
        public string Name { get; set; } = string.Empty;
        
        [Sensitive(SensitivityType.Secret)]
        public string Password { get; set; } = string.Empty;
    }

    private class TestResponse
    {
        public string Result { get; set; } = string.Empty;
    }
}
