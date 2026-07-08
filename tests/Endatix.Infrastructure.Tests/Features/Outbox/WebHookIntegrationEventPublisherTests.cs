using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// Unit tests for the Stage-1 webhook publisher: dotted EventType → WebHookOperation mapping, formId
/// extraction from the stored (string-id) payload, stable hook id (= outbox row Id), tenant routing, and
/// failure handling (a failed delivery throws so the relay retries).
/// </summary>
public class WebHookIntegrationEventPublisherTests
{
    private readonly IWebHookService _webHooks = Substitute.For<IWebHookService>();

    public WebHookIntegrationEventPublisherTests()
    {
        // Default: delivery succeeds. Individual tests override for the failure path.
        _webHooks.DeliverWebHookAsync(
            Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(true);
    }

    private WebHookOutboxIntegrationEventHandler CreateSut() =>
        new(_webHooks, NullLogger<WebHookOutboxIntegrationEventHandler>.Instance);

    [Theory]
    [InlineData("form.created", "form_created")]
    [InlineData("form.updated", "form_updated")]
    [InlineData("form.enabled_state_changed", "form_enabled_state_changed")]
    [InlineData("submission.completed", "submission_completed")]
    [InlineData("form.deleted", "form_deleted")]
    public async Task Maps_event_type_and_delivers_with_stable_hook_id_tenant_and_formId(string eventType, string expectedOperation)
    {
        // Arrange
        var message = new FakeOutboxMessage(
            Id: 777, EventType: eventType, Payload: """{"formId":"555","name":"x"}""", TenantId: 42);

        // Act
        await CreateSut().HandleAsync(message, CancellationToken.None);

        // Assert
        await _webHooks.Received(1).DeliverWebHookAsync(
            42L,
            Arg.Is<WebHookMessage<JsonElement>>(m => m.id == 777L && m.operation.EventName == expectedOperation),
            Arg.Any<CancellationToken>(),
            555L);
    }

    [Fact]
    public async Task Every_slice_events_real_EventType_round_trips_to_a_delivery()
    {
        // Guards against drift between an event's EventType literal (capture side) and the publisher's
        // mapping derived from WebHookOperation.EventName (lookup side): a mismatch would silently skip
        // delivery. Drives the publisher with the EventType + payload the ACTUAL event classes produce.
        const long formId = 555L;
        var form = new Form(tenantId: 42, name: "f") { Id = formId };
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 42,
            FormId: formId,
            FormDefinitionId: 1,
            JsonData: "{}",
            IsComplete: true));

        IIntegrationEvent[] events =
        [
            new FormCreatedEvent(form),
            new FormUpdatedEvent(form),
            new FormEnabledStateChangedEvent(form, isEnabled: true),
            new FormDeletedEvent(form),
            new SubmissionCompletedEvent(submission),
        ];

        var sut = CreateSut();
        foreach (var integrationEvent in events)
        {
            var payload = JsonSerializer.Serialize(integrationEvent.GetPayload());
            var message = new FakeOutboxMessage(Id: 1, EventType: integrationEvent.EventType, Payload: payload, TenantId: 42);

            await sut.HandleAsync(message, CancellationToken.None);
        }

        // Every event's EventType must have mapped → been delivered for the right form (none skipped).
        await _webHooks.Received(events.Length).DeliverWebHookAsync(
            42L, Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), formId);
    }

    [Fact]
    public async Task Unmapped_event_type_is_skipped_and_not_delivered()
    {
        // Arrange
        var message = new FakeOutboxMessage(Id: 1, EventType: "form.archived", Payload: "{}", TenantId: 1);

        // Act
        await CreateSut().HandleAsync(message, CancellationToken.None);

        // Assert
        await _webHooks.DidNotReceive().DeliverWebHookAsync(
            Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task Missing_formId_throws_and_does_not_deliver()
    {
        // Arrange — a mapped (form-scoped) event whose payload has no formId is malformed.
        var message = new FakeOutboxMessage(Id: 9, EventType: "form.created", Payload: """{"name":"no-form-id"}""", TenantId: 7);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().HandleAsync(message, CancellationToken.None));
        await _webHooks.DidNotReceive().DeliverWebHookAsync(
            Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task Delivery_failure_throws_so_the_relay_retries()
    {
        // Arrange
        _webHooks.DeliverWebHookAsync(
            Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(false);
        var message = new FakeOutboxMessage(Id: 5, EventType: "form.created", Payload: """{"formId":"555"}""", TenantId: 1);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().HandleAsync(message, CancellationToken.None));
    }

    private sealed record FakeOutboxMessage(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
