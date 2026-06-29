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
    public void FormEnabledStateChangedEvent_has_dotted_type_and_omits_folderId()
    {
        var form = new Form(tenantId: 1, name: "Test") { Id = 7 };
        var evt = new FormEnabledStateChangedEvent(form, isEnabled: true);

        evt.EventType.Should().Be("form.enabled_state_changed");
        var json = Payload(evt.GetPayload());
        json.GetProperty("formId").GetInt64().Should().Be(7);
        json.TryGetProperty("revision", out _).Should().BeTrue();
        json.TryGetProperty("folderId", out _).Should().BeFalse("enabled-state payload omits folderId");
    }

    [Fact]
    public void FormDeletedEvent_has_dotted_type_and_omits_folderId()
    {
        var form = new Form(tenantId: 1, name: "Test") { Id = 9 };
        var evt = new FormDeletedEvent(form);

        evt.EventType.Should().Be("form.deleted");
        var json = Payload(evt.GetPayload());
        json.TryGetProperty("folderId", out _).Should().BeFalse();
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
