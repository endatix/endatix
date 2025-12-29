using Endatix.Core.Infrastructure.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Tests.Infrastructure.Logging;

public class LoggingPipelineBehaviorTests
{
    private readonly ILogger<Mediator> _logger;
    private readonly LoggingPipelineBehavior<TestRequest, TestResponse> _sut;

    public LoggingPipelineBehaviorTests()
    {
        _logger = Substitute.For<ILogger<Mediator>>();
        _sut = new LoggingPipelineBehavior<TestRequest, TestResponse>(_logger);
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
    public async Task Handle_LoggingDisabled_OnlyLogsResponseTime()
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
        
        logCalls.Should().HaveCount(1);
        logCalls[0].Should().StartWith($"Handled TestRequest with {response} in");
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
