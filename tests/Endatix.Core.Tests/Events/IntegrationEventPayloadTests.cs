using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Events;

/// <summary>
/// Phase 3b: each slice event's stable <c>EventType</c> + <c>GetPayload()</c> shape (the payload the relay
/// serializes to the outbox and delivers as a webhook), including the per-aggregate <c>revision</c>.
/// </summary>
public class IntegrationEventPayloadTests
{
    private static JsonElement Payload(object dto) => JsonSerializer.SerializeToElement(dto);

    [Fact]
    public void FormCreatedEvent_has_dotted_type_and_form_payload_with_revision_and_folderId()
    {
        var form = new Form(tenantId: 1, name: "Test", description: "d", isEnabled: true) { Id = 555 };
        var evt = new FormCreatedEvent(form);

        evt.EventType.Should().Be("form.created");
        var json = Payload(evt.GetPayload());
        json.GetProperty("formId").GetInt64().Should().Be(555);
        json.GetProperty("tenantId").GetInt64().Should().Be(1);
        json.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        json.GetProperty("revision").GetInt64().Should().Be(1);
        json.TryGetProperty("folderId", out _).Should().BeTrue();
    }

    [Fact]
    public void FormEnabledStateChangedEvent_uses_its_captured_enabled_state_and_includes_folderId()
    {
        // The live form is still disabled; the event captured isEnabled: true. The payload must reflect the
        // captured (event-creation-time) value, not the live form.
        var form = new Form(tenantId: 1, name: "Test") { Id = 7 };
        var evt = new FormEnabledStateChangedEvent(form, isEnabled: true);

        evt.EventType.Should().Be("form.enabled_state_changed");
        var json = Payload(evt.GetPayload());
        json.GetProperty("formId").GetInt64().Should().Be(7);
        json.GetProperty("isEnabled").GetBoolean().Should().BeTrue("payload uses the event's captured enabled state");
        json.TryGetProperty("revision", out _).Should().BeTrue();
        json.TryGetProperty("folderId", out _).Should().BeTrue("all form events carry folderId");
    }

    [Fact]
    public void FormDeletedEvent_has_dotted_type_and_includes_folderId()
    {
        var form = new Form(tenantId: 1, name: "Test") { Id = 9 };
        var evt = new FormDeletedEvent(form);

        evt.EventType.Should().Be("form.deleted");
        var json = Payload(evt.GetPayload());
        json.TryGetProperty("folderId", out _).Should().BeTrue();
    }

    [Fact]
    public void Events_queued_in_one_transaction_keep_their_own_revision()
    {
        // An update that toggles enabled raises two events: enabled_state_changed (revision 2) then
        // updated (revision 3). Each payload must keep the revision captured when it was raised, not the
        // final live value — otherwise an order-sensitive consumer can't distinguish/order them.
        var form = new Form(tenantId: 1, name: "Test") { Id = 1 }; // revision 1
        form.SetEnabled(true); // revision 2, raises enabled_state_changed
        form.UpdateDetails("New", null, isPublic: true, limitOnePerUser: false, metadata: null); // revision 3, raises updated

        var enabled = form.DomainEvents.OfType<FormEnabledStateChangedEvent>().Single();
        var updated = form.DomainEvents.OfType<FormUpdatedEvent>().Single();

        Payload(enabled.GetPayload()).GetProperty("revision").GetInt64().Should().Be(2);
        Payload(updated.GetPayload()).GetProperty("revision").GetInt64().Should().Be(3);
    }

    [Fact]
    public void SubmissionCompletedEvent_has_dotted_type_and_submission_payload_with_revision()
    {
        var submission = Submission.Create(1, "{}", formId: 100, formDefinitionId: 200,
            new SubmissionCreateOptions(IsComplete: true));
        var evt = new SubmissionCompletedEvent(submission);

        evt.EventType.Should().Be("submission.completed");
        var json = Payload(evt.GetPayload());
        json.GetProperty("formId").GetInt64().Should().Be(100);
        json.GetProperty("formDefinitionId").GetInt64().Should().Be(200);
        json.GetProperty("isComplete").GetBoolean().Should().BeTrue();
        json.TryGetProperty("revision", out _).Should().BeTrue();
        json.TryGetProperty("completedAt", out _).Should().BeTrue();
    }
}
