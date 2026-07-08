using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

public class CompositeIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithMatchingHandlers_InvokesAllSubscribedHandlers()
    {
        RecordingOutboxHandler webhookHandler = new(["submission.completed"]);
        RecordingOutboxHandler reportingHandler = new(["submission.completed", "submission.updated"]);
        RecordingOutboxHandler otherHandler = new(["form.created"]);
        CompositeIntegrationEventPublisher publisher = new(
            [webhookHandler, reportingHandler, otherHandler],
            NullLogger<CompositeIntegrationEventPublisher>.Instance);
        IOutboxMessage message = new FakeOutboxMessage(1, "submission.completed", "{}", 1);

        await publisher.PublishAsync(message, TestContext.Current.CancellationToken);

        webhookHandler.HandledIds.Should().ContainSingle().Which.Should().Be(1);
        reportingHandler.HandledIds.Should().ContainSingle().Which.Should().Be(1);
        otherHandler.HandledIds.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WithNoMatchingHandler_CompletesWithoutThrowing()
    {
        CompositeIntegrationEventPublisher publisher = new(
            [new RecordingOutboxHandler(["form.created"])],
            NullLogger<CompositeIntegrationEventPublisher>.Instance);
        IOutboxMessage message = new FakeOutboxMessage(2, "submission.deleted", "{}", 1);

        Func<Task> act = () => publisher.PublishAsync(message, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerFails_StopsAndDoesNotInvokeRemainingHandlers()
    {
        RecordingOutboxHandler succeedingHandler = new(["submission.completed"]);
        FailingOutboxHandler failingHandler = new(["submission.completed"]);
        RecordingOutboxHandler neverReachedHandler = new(["submission.completed"]);
        CompositeIntegrationEventPublisher publisher = new(
            [succeedingHandler, failingHandler, neverReachedHandler],
            NullLogger<CompositeIntegrationEventPublisher>.Instance);
        IOutboxMessage message = new FakeOutboxMessage(3, "submission.completed", "{}", 1);

        Func<Task> act = () => publisher.PublishAsync(message, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        succeedingHandler.HandledIds.Should().ContainSingle().Which.Should().Be(3);
        neverReachedHandler.HandledIds.Should().BeEmpty();
    }

    private sealed class FailingOutboxHandler(IReadOnlyCollection<string> eventTypes) : IOutboxIntegrationEventHandler
    {
        public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Handler failed.");
    }

    private sealed class RecordingOutboxHandler(IReadOnlyCollection<string> eventTypes) : IOutboxIntegrationEventHandler
    {
        public List<long> HandledIds { get; } = [];

        public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;

        public Task HandleAsync(IOutboxMessage message, CancellationToken cancellationToken)
        {
            HandledIds.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    private sealed record FakeOutboxMessage(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
