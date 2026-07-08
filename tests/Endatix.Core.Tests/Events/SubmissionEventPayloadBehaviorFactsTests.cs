using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Events;

/// <summary>
/// Behavior facts for submission event payload records (collocated nested types) and their wire shapes.
/// </summary>
public class SubmissionEventPayloadBehaviorFactsTests
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    private static JsonElement WirePayload(object dto) =>
        JsonSerializer.SerializeToElement(dto, dto.GetType(), WireOptions);

    private static Submission CreateCompleteSubmission() =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 100,
            FormDefinitionId: 200,
            JsonData: """{"q1":"a"}""",
            IsComplete: true,
            CurrentPage: 3,
            Metadata: """{"tag":"vip"}""",
            SubmitterId: 42,
            SubmitterDisplayId: "display-42"));

    [Fact]
    public void SubmissionCompletedEvent_Payload_FromSubmission_MapsAllFields()
    {
        Submission submission = CreateCompleteSubmission();
        submission.Id = 501;

        SubmissionCompletedEvent.Payload payload = new(submission, revision: 2);

        payload.SubmissionId.Should().Be(501);
        payload.FormId.Should().Be(100);
        payload.FormDefinitionId.Should().Be(200);
        payload.TenantId.Should().Be(1);
        payload.IsComplete.Should().BeTrue();
        payload.JsonData.Should().Be("""{"q1":"a"}""");
        payload.CurrentPage.Should().Be(3);
        payload.Metadata.Should().Be("""{"tag":"vip"}""");
        payload.SubmittedBy.Should().Be("42");
        payload.SubmitterDisplayId.Should().Be("display-42");
        payload.Status.Should().Be("new");
        payload.Revision.Should().Be(2);
        payload.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void SubmissionUpdatedEvent_Payload_WithChangeKind_InheritsBaseAndSetsChangeKind()
    {
        Submission submission = CreateCompleteSubmission();

        SubmissionUpdatedEvent.Payload payload = new(
            submission,
            revision: 3,
            SubmissionChangeKinds.Answers | SubmissionChangeKinds.Metadata);

        payload.Should().BeAssignableTo<SubmissionCompletedEvent.Payload>();
        payload.ChangeKind.Should().Be("answers,metadata");
        payload.FormId.Should().Be(100);
    }

    [Fact]
    public void SubmissionStatusChangedEvent_Payload_WithPreviousStatus_InheritsBaseAndSetsPreviousStatus()
    {
        Submission submission = CreateCompleteSubmission();

        SubmissionStatusChangedEvent.Payload payload = new(submission, revision: 4, SubmissionStatus.New);

        payload.Should().BeAssignableTo<SubmissionCompletedEvent.Payload>();
        payload.PreviousStatus.Should().Be("new");
        payload.Status.Should().Be("new");
    }

    [Fact]
    public void SubmissionCompletedEvent_GetPayload_OnWire_ExcludesChangeKindAndPreviousStatus()
    {
        Submission submission = CreateCompleteSubmission();
        JsonElement json = WirePayload(new SubmissionCompletedEvent(submission).GetPayload());

        json.TryGetProperty("changeKind", out _).Should().BeFalse();
        json.TryGetProperty("previousStatus", out _).Should().BeFalse();
        json.GetProperty("formId").GetInt64().Should().Be(100);
        json.GetProperty("revision").GetInt64().Should().BeGreaterThan(1);
    }

    [Fact]
    public void SubmissionUpdatedEvent_GetPayload_OnWire_IncludesChangeKindOnly()
    {
        Submission submission = CreateCompleteSubmission();
        submission.ClearDomainEvents();
        submission.Update(
            """{"q1":"b"}""",
            200,
            formDefinitionFormId: 100,
            isComplete: true,
            metadata: """{"tag":"vip"}""");

        SubmissionUpdatedEvent updated = submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Single();
        JsonElement json = WirePayload(updated.GetPayload());

        json.GetProperty("changeKind").GetString().Should().Be("answers");
        json.TryGetProperty("previousStatus", out _).Should().BeFalse();
        updated.GetPayload().Should().BeOfType<SubmissionUpdatedEvent.Payload>();
    }

    [Fact]
    public void SubmissionStatusChangedEvent_GetPayload_OnWire_IncludesPreviousAndCurrentStatus()
    {
        Submission submission = CreateCompleteSubmission();
        submission.ClearDomainEvents();
        submission.UpdateStatus(SubmissionStatus.Approved);

        SubmissionStatusChangedEvent statusChanged = submission.DomainEvents.OfType<SubmissionStatusChangedEvent>().Single();
        JsonElement json = WirePayload(statusChanged.GetPayload());

        json.GetProperty("previousStatus").GetString().Should().Be("new");
        json.GetProperty("status").GetString().Should().Be("approved");
        json.TryGetProperty("changeKind", out _).Should().BeFalse();
        statusChanged.GetPayload().Should().BeOfType<SubmissionStatusChangedEvent.Payload>();
    }

    [Fact]
    public void SubmissionEventPayload_OnWire_IncludesCoreFieldsForAllVariants()
    {
        Submission submission = CreateCompleteSubmission();
        submission.ClearDomainEvents();
        submission.Update(
            """{"q1":"b"}""",
            200,
            formDefinitionFormId: 100,
            isComplete: true,
            metadata: """{"tag":"vip"}""");
        submission.UpdateStatus(SubmissionStatus.Approved);

        object[] payloads =
        [
            new SubmissionCompletedEvent.Payload(submission, revision: 2),
            submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Single().GetPayload(),
            submission.DomainEvents.OfType<SubmissionStatusChangedEvent>().Single().GetPayload(),
        ];

        foreach (JsonElement json in payloads.Select(WirePayload))
        {
            json.TryGetProperty("submissionId", out _).Should().BeTrue();
            json.TryGetProperty("formId", out _).Should().BeTrue();
            json.TryGetProperty("formDefinitionId", out _).Should().BeTrue();
            json.TryGetProperty("tenantId", out _).Should().BeTrue();
            json.TryGetProperty("jsonData", out _).Should().BeTrue();
            json.TryGetProperty("revision", out _).Should().BeTrue();
        }
    }

    [Fact]
    public void SubmissionUpdatedEvent_Payload_WireJson_RoundTripsSuccessfully()
    {
        Submission submission = CreateCompleteSubmission();
        SubmissionUpdatedEvent.Payload original = new(submission, revision: 5, SubmissionChangeKinds.Definition);
        string wireJson = JsonSerializer.Serialize(original, original.GetType(), WireOptions);

        wireJson.Should().Contain("\"changeKind\":\"definition\"");
        wireJson.Should().Contain("\"formId\":");
        wireJson.Should().Contain("\"revision\":5");
    }
}
