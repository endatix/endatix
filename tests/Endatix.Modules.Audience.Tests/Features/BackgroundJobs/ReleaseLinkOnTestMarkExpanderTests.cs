using Endatix.Modules.Audience.Features.BackgroundJobs;
using Endatix.Outbox.Engine;

namespace Endatix.Modules.Audience.Tests.Features.BackgroundJobs;

public sealed class ReleaseLinkOnTestMarkExpanderTests
{
    private readonly ReleaseLinkOnTestMarkExpander _expander = new();

    [Theory]
    [InlineData("test", 1)]
    [InlineData("answers", 0)]
    [InlineData("metadata", 0)]
    public async Task ExpandAsync_SubmissionUpdate_EnqueuesTheReleaseOnlyForATestMark(string changeKind, int expectedJobs)
    {
        // Arrange
        var message = new Message(
            7, $$"""{"submissionId":"20","tenantId":"1","changeKind":"{{changeKind}}"}""");

        // Act
        var subscribers = await _expander.ExpandAsync(message, 1, TestContext.Current.CancellationToken);

        // Assert
        subscribers.Select(subscriber => subscriber.Payload)
            .Should().Equal(Enumerable.Repeat(new AudienceReleaseLinkOnTestMarkPayload(7), expectedJobs));
    }

    [Fact]
    public async Task ExpandAsync_UnreadablePayload_EnqueuesSoTheJobFailsVisibly()
    {
        // Arrange
        var message = new Message(8, "not json");

        // Act
        var subscribers = await _expander.ExpandAsync(message, 1, TestContext.Current.CancellationToken);

        // Assert
        subscribers.Should().ContainSingle();
    }

    private sealed record Message(long Id, string Payload) : IOutboxMessage
    {
        public string EventType => "submission.updated";
        public long TenantId => 1;
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
