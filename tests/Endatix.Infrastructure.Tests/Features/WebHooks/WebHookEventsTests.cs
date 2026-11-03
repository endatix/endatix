using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
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
/// The webhook contract stays as documented while the entity raises more events: the same event types are
/// subscribed, <c>submission.created</c> is not a webhook, and <c>submission_collection_status_changed</c> is
/// delivered only for a terminal outcome other than complete.
/// </summary>
public sealed class WebHookEventsTests
{
    private const long TenantId = 42;
    private const long FormId = 555;

    [Fact]
    public void EventTypes_AreTheDocumentedWebHookEvents()
    {
        WebHookEvents.EventTypes.Should().BeEquivalentTo(
            "form.created",
            "form.updated",
            "form.enabled_state_changed",
            "submission.completed",
            "submission.collection_status_changed",
            "form.deleted");
    }

    [Theory]
    [InlineData(CollectionStatusCodes.ScreenOut, true)]
    [InlineData(CollectionStatusCodes.QuotaFull, true)]
    [InlineData(CollectionStatusCodes.Expired, true)]
    [InlineData(CollectionStatusCodes.Abandoned, true)]
    [InlineData(CollectionStatusCodes.Cancelled, true)]
    [InlineData(CollectionStatusCodes.Complete, false)]
    [InlineData(CollectionStatusCodes.InProgress, false)]
    [InlineData(CollectionStatusCodes.NotStarted, false)]
    [InlineData(CollectionStatusCodes.Viewed, false)]
    [InlineData("custom_code", false)]
    public void TryGetOperation_CollectionStatusChanged_IsAWebHookOnlyForATerminalOutcomeOtherThanComplete(
        string collectionStatus,
        bool delivered)
    {
        var message = Message(
            SubmissionCollectionStatusChangedEvent.EventTypeName,
            $$"""{"formId":"555","collectionStatus":"{{collectionStatus}}"}""");

        var isWebHook = WebHookEvents.TryGetOperation(message.EventType, message.Payload, out var operation);

        isWebHook.Should().Be(delivered);
        if (delivered)
        {
            operation.Should().Be(WebHookOperation.SubmissionCollectionStatusChanged);
        }
    }

    [Fact]
    public void TryGetOperation_RealTransitions_DeliverTheScreenOutTheCancelAndTheCompletedEvent()
    {
        // Every transition the entity raises on the way to each outcome it can reach, as captured to the outbox.
        var prefilledThenScreenedOut = NewSubmission(id: 1, startSubmission: false);
        prefilledThenScreenedOut.EnsureStarted();
        prefilledThenScreenedOut.ScreenOut();
        var completed = NewSubmission(id: 2, startSubmission: false);
        completed.Update("""{"a":1}""", 1, FormId, isComplete: false);
        completed.Update("""{"a":1}""", 1, FormId, isComplete: true);
        var cancelled = NewSubmission(id: 3, startSubmission: true);
        cancelled.Cancel();

        var captured = new OutboxIntegrationEventDispatcher().Capture([prefilledThenScreenedOut, completed, cancelled]);

        var webHooks = captured
            .Select(row => Message(row.EventType, row.Payload))
            .Where(message => WebHookEvents.TryGetOperation(message.EventType, message.Payload, out _))
            .Select(message => (message.EventType, CollectionStatusOf(message)))
            .ToList();
        webHooks.Should().Equal(
            (SubmissionCollectionStatusChangedEvent.EventTypeName, CollectionStatusCodes.ScreenOut),
            (SubmissionCompletedEvent.EventTypeName, null),
            (SubmissionCollectionStatusChangedEvent.EventTypeName, CollectionStatusCodes.Cancelled));
        captured.Select(row => row.EventType).Should().Contain(SubmissionCreatedEvent.EventTypeName);
    }

    [Fact]
    public async Task InlineHandler_SkipsACollectionStatusChangeToComplete_AndDeliversAScreenOut()
    {
        var webHookService = Substitute.For<IWebHookService>();
        webHookService.DeliverWebHookAsync(
                Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(true);
        WebHookOutboxIntegrationEventHandler handler = new(
            webHookService, NullLogger<WebHookOutboxIntegrationEventHandler>.Instance);

        await handler.HandleAsync(
            Message(SubmissionCollectionStatusChangedEvent.EventTypeName, """{"formId":"555","collectionStatus":"complete"}"""),
            CancellationToken.None);
        await handler.HandleAsync(
            Message(SubmissionCollectionStatusChangedEvent.EventTypeName, """{"formId":"555","collectionStatus":"screen_out"}"""),
            CancellationToken.None);

        await webHookService.Received(1).DeliverWebHookAsync(
            TenantId,
            Arg.Is<WebHookMessage<JsonElement>>(m => m.operation == WebHookOperation.SubmissionCollectionStatusChanged),
            Arg.Any<CancellationToken>(),
            FormId);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.ScreenOut, 1)]
    [InlineData(CollectionStatusCodes.Complete, 0)]
    [InlineData(CollectionStatusCodes.NotStarted, 0)]
    public async Task Expander_QueuesAJobOnlyForATerminalOutcomeOtherThanComplete(string collectionStatus, int jobs)
    {
        var forms = Substitute.For<IRepository<Form>>();
        var tenantSettings = Substitute.For<IRepository<TenantSettings>>();
        tenantSettings
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new TenantSettings(TenantId, webHookSettingsJson: JsonSerializer.Serialize(new WebHookConfiguration
            {
                Events = new Dictionary<string, WebHookEventConfig>
                {
                    ["SubmissionCollectionStatusChanged"] = new()
                    {
                        IsEnabled = true,
                        WebHookEndpoints = [new WebHookEndpointConfig { Url = "https://hooks.example.test/receive" }],
                    },
                },
            })));
        WebHookEndpointExpander expander = new(
            new WebHookEventConfigReader(forms, tenantSettings, NullLogger<WebHookEventConfigReader>.Instance));

        var subscribers = await expander.ExpandAsync(
            Message(
                SubmissionCollectionStatusChangedEvent.EventTypeName,
                $$"""{"formId":"555","collectionStatus":"{{collectionStatus}}"}"""),
            TenantId,
            CancellationToken.None);

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

    private static string? CollectionStatusOf(IOutboxMessage message)
    {
        using var document = JsonDocument.Parse(message.Payload);
        return document.RootElement.TryGetProperty("collectionStatus", out var status) ? status.GetString() : null;
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
