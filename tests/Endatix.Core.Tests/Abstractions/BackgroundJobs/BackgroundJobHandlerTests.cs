using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public sealed class BackgroundJobHandlerTests
{
    [Theory]
    [InlineData("{not json")]
    [InlineData("{}")]
    public async Task ExecuteAsync_WithUnreadablePayload_ReturnsFailureWithoutCallingHandler(string payloadJson)
    {
        // Arrange
        var handler = new RecordingHandler(NullLogger.Instance);
        var job = new BackgroundJobContext(1, "Test", 5, payloadJson, 1);

        // Act
        var result = await handler.ExecuteAsync(job, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The job's input could not be read (Test).");
        handler.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithReadablePayload_PassesTypedPayloadToHandler()
    {
        // Arrange
        var handler = new RecordingHandler(NullLogger.Instance);
        var job = new BackgroundJobContext(1, "Test", 5, "{\"id\":42}", 1);

        // Act
        var result = await handler.ExecuteAsync(job, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        handler.Received.Should().Equal(new TestPayload(42));
    }

    [Fact]
    public void JobType_DerivedHandler_IsPayloadJobType()
    {
        // Arrange
        var handler = new RecordingHandler(NullLogger.Instance);

        // Act
        var jobType = handler.JobType;

        // Assert
        jobType.Should().Be(TestPayload.JobType);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnreadablePayload_LogsWarningWithReaderError()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        logger.IsEnabled(LogLevel.Warning).Returns(true);
        var handler = new RecordingHandler(logger);
        var job = new BackgroundJobContext(1, "Test", 5, "{}", 1);

        // Act
        await handler.ExecuteAsync(job, CancellationToken.None);

        // Assert
        var logCall = logger.ReceivedCalls().Should().ContainSingle(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Subject.GetArguments();
        logCall[0].Should().Be(LogLevel.Warning);
        logCall[3].Should().BeAssignableTo<System.Text.Json.JsonException>();
    }

    private sealed class RecordingHandler(ILogger logger) : BackgroundJobHandler<TestPayload>(logger)
    {
        public List<TestPayload> Received { get; } = [];

        protected override Task<Result> ExecuteAsync(
            BackgroundJobContext job,
            TestPayload payload,
            CancellationToken cancellationToken)
        {
            Received.Add(payload);
            return Task.FromResult(Result.Success());
        }
    }
}
