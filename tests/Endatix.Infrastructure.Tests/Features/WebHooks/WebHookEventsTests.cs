using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

/// <summary>
/// Webhook delivery depends on the event type only: <c>submission.completed</c> and <c>submission.closed</c> are
/// webhooks, every other lifecycle event is not, and no payload is read to decide.
/// </summary>
public sealed class WebHookEventsTests
{
    private const long TenantId = 42;
    private const long FormId = 555;

    [Fact]
    public void EventTypes_AreTheDocumentedWebHookEvents()
    {
        // Act
        var eventTypes = WebHookEvents.EventTypes;

        // Assert
        eventTypes.Should().BeEquivalentTo(
            "form.created",
            "form.updated",
            "form.enabled_state_changed",
            "submission.completed",
            "submission.closed",
            "form.deleted");
    }

    [Theory]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Created, false)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Started, false)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Completed, true)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Closed, true)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Lapsed, false)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Resumed, false)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.CollectionStatusMoved, false)]
    [InlineData("submission.collection_status_changed", false)]
    public void TryGetOperation_LifecycleEventType_IsAWebHookByTypeAlone(string eventType, bool delivered)
    {
        // Act
        var isWebHook = WebHookEvents.TryGetOperation(eventType, out _);

        // Assert
        isWebHook.Should().Be(delivered);
    }

    [Fact]
    public void TryGetOperation_SubmissionClosed_IsTheSubmissionClosedOperation()
    {
        // Act
        WebHookEvents.TryGetOperation(SubmissionLifecycleEvent.EventTypes.Closed, out var operation);

        // Assert
        operation.Should().Be(WebHookOperation.SubmissionClosed);
    }

    [Fact]
    public void TryGetOperation_CapturedLifecycleRows_DeliverTheClosesAndTheCompletion()
    {
        // Arrange
        var prefilledThenScreenedOut = NewSubmission(id: 1, startSubmission: false);
        prefilledThenScreenedOut.EnsureStarted();
        prefilledThenScreenedOut.ScreenOut();
        var completed = NewSubmission(id: 2, startSubmission: false);
        completed.Update("""{"a":1}""", 1, FormId, isComplete: false);
        completed.Update("""{"a":1}""", 1, FormId, isComplete: true);
        var cancelled = NewSubmission(id: 3, startSubmission: true);
        cancelled.Cancel();

        // Act
        var captured = OutboxIntegrationEventDispatcher.Capture([prefilledThenScreenedOut, completed, cancelled]);

        // Assert
        captured.Select(row => row.EventType).Should().Equal(
            "submission.created", "submission.started", "submission.closed",
            "submission.created", "submission.started", "submission.completed",
            "submission.created", "submission.closed");
        captured.Where(row => WebHookEvents.TryGetOperation(row.EventType, out _))
            .Select(row => (row.EventType, CollectionStatusOf(row.Payload)))
            .Should().Equal(
                ("submission.closed", CollectionStatusCodes.ScreenOut),
                ("submission.completed", CollectionStatusCodes.Complete),
                ("submission.closed", CollectionStatusCodes.Cancelled));
    }

    [Fact]
    public async Task InlineHandler_StartedAndClosed_DeliversOnlyTheClosedWithoutParsingToDecide()
    {
        // Arrange
        var webHookService = Substitute.For<IWebHookService>();
        webHookService.DeliverWebHookAsync(
                Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(true);
        WebHookOutboxIntegrationEventHandler handler = new(
            webHookService, NullLogger<WebHookOutboxIntegrationEventHandler>.Instance);

        // Act
        await handler.HandleAsync(Message(SubmissionLifecycleEvent.EventTypes.Started, "not json"), CancellationToken.None);
        await handler.HandleAsync(Message(SubmissionLifecycleEvent.EventTypes.Closed, """{"formId":"555"}"""), CancellationToken.None);

        // Assert
        await webHookService.Received(1).DeliverWebHookAsync(
            TenantId,
            Arg.Is<WebHookMessage<JsonElement>>(m => m.operation == WebHookOperation.SubmissionClosed),
            Arg.Any<CancellationToken>(),
            FormId);
    }

    [Theory]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Closed, 1)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Lapsed, 0)]
    [InlineData(SubmissionLifecycleEvent.EventTypes.Started, 0)]
    public async Task Expander_LifecycleEventType_QueuesAJobOnlyForAWebHookType(string eventType, int jobs)
    {
        // Arrange
        var forms = Substitute.For<IRepository<Form>>();
        var tenantSettings = Substitute.For<IRepository<TenantSettings>>();
        tenantSettings
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new TenantSettings(TenantId, webHookSettingsJson: JsonSerializer.Serialize(new WebHookConfiguration
            {
                Events = new Dictionary<string, WebHookEventConfig>
                {
                    ["SubmissionClosed"] = new()
                    {
                        IsEnabled = true,
                        WebHookEndpoints = [new WebHookEndpointConfig { Url = "https://hooks.example.test/receive" }],
                    },
                },
            })));
        WebHookEndpointExpander expander = new(
            new WebHookEventConfigReader(forms, tenantSettings, NullLogger<WebHookEventConfigReader>.Instance));

        // Act
        var subscribers = await expander.ExpandAsync(
            Message(eventType, """{"formId":"555"}"""), TenantId, CancellationToken.None);

        // Assert
        subscribers.Should().HaveCount(jobs);
    }

    private static Submission NewSubmission(long id, bool startSubmission)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: false,
            StartSubmission: startSubmission));
        submission.Id = id;
        return submission;
    }

    private static string? CollectionStatusOf(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("collectionStatus").GetProperty("code").GetString();
    }

    private static FakeOutboxMessage Message(string eventType, string payload) =>
        new(Id: 1, EventType: eventType, Payload: payload, TenantId: TenantId);

    private sealed record FakeOutboxMessage(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
