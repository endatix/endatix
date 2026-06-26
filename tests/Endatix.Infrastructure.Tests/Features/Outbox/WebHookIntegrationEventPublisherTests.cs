using System.Text.Json;
using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>
/// Unit tests for the Stage-1 webhook publisher: dotted EventType → WebHookOperation mapping, formId
/// extraction from the stored (string-id) payload, stable hook id (= outbox row Id), and tenant routing.
/// </summary>
public class WebHookIntegrationEventPublisherTests
{
    private readonly IWebHookService _webHooks = Substitute.For<IWebHookService>();

    private WebHookIntegrationEventPublisher CreateSut() =>
        new(_webHooks, NullLogger<WebHookIntegrationEventPublisher>.Instance);

    [Theory]
    [InlineData("form.created", "form_created")]
    [InlineData("form.updated", "form_updated")]
    [InlineData("form.enabled_state_changed", "form_enabled_state_changed")]
    [InlineData("submission.completed", "submission_completed")]
    [InlineData("form.deleted", "form_deleted")]
    public async Task Maps_event_type_and_enqueues_with_stable_hook_id_tenant_and_formId(string eventType, string expectedOperation)
    {
        // Arrange
        var message = new FakeOutboxMessage(
            Id: 777, EventType: eventType, Payload: """{"formId":"555","name":"x"}""", TenantId: 42);

        // Act
        await CreateSut().PublishAsync(message, CancellationToken.None);

        // Assert
        await _webHooks.Received(1).EnqueueWebHookAsync(
            42L,
            Arg.Is<WebHookMessage<JsonElement>>(m => m.id == 777L && m.operation.EventName == expectedOperation),
            Arg.Any<CancellationToken>(),
            555L);
    }

    [Fact]
    public async Task Unmapped_event_type_is_skipped_and_not_enqueued()
    {
        // Arrange
        var message = new FakeOutboxMessage(Id: 1, EventType: "form.archived", Payload: "{}", TenantId: 1);

        // Act
        await CreateSut().PublishAsync(message, CancellationToken.None);

        // Assert
        await _webHooks.DidNotReceive().EnqueueWebHookAsync(
            Arg.Any<long>(), Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), Arg.Any<long?>());
    }

    [Fact]
    public async Task Missing_formId_passes_null_formId()
    {
        // Arrange
        var message = new FakeOutboxMessage(Id: 9, EventType: "form.created", Payload: """{"name":"no-form-id"}""", TenantId: 7);

        // Act
        await CreateSut().PublishAsync(message, CancellationToken.None);

        // Assert
        await _webHooks.Received(1).EnqueueWebHookAsync(
            7L, Arg.Any<WebHookMessage<JsonElement>>(), Arg.Any<CancellationToken>(), null);
    }

    private sealed record FakeOutboxMessage(long Id, string EventType, string Payload, long TenantId) : IOutboxMessage
    {
        public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;
        public int SchemaVersion => 1;
        public int Attempts => 0;
        public string? TraceId => null;
    }
}
