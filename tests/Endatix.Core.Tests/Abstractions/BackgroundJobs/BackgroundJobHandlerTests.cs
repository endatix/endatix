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

    [Fact]
    public async Task ExecuteAsync_PayloadTypeReaderCannotBind_ReturnsFailureWithoutCallingHandler()
    {
        // Arrange — the input is exactly what enqueueing wrote, yet the type can never be read back.
        var handler = new UnbindableHandler(NullLogger.Instance);
        var payloadJson = BackgroundJobPayloadSerializer.Serialize(new UnbindablePayload(7));
        var job = new BackgroundJobContext(1, UnbindablePayload.JobType, 5, payloadJson, 1);

        // Act
        var result = await handler.ExecuteAsync(job, CancellationToken.None);

        // Assert — a failure, which the job records on its first attempt, rather than a throw that would retry.
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("The job's input could not be read (Unbindable).");
        handler.Calls.Should().Be(0);
    }

    private sealed class UnbindableHandler(ILogger logger) : BackgroundJobHandler<UnbindablePayload>(logger)
    {
        public int Calls { get; private set; }

        protected override Task<Result> ExecuteAsync(
            BackgroundJobContext job,
            UnbindablePayload payload,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result.Success());
        }
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
