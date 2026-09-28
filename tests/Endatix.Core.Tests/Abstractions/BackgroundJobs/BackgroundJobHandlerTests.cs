using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Tests.Abstractions.BackgroundJobs;

public sealed class BackgroundJobHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithUnreadablePayload_ReturnsFailureWithoutCallingHandler()
    {
        // Arrange
        var handler = new RecordingHandler();
        var job = new BackgroundJobContext(1, "Test", 5, "{not json", 1);

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
        var handler = new RecordingHandler();
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
        var handler = new RecordingHandler();

        // Act
        var jobType = handler.JobType;

        // Assert
        jobType.Should().Be(TestPayload.JobType);
    }

    private sealed class RecordingHandler : BackgroundJobHandler<TestPayload>
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
