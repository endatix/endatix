using System.Reflection;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using FluentAssertions;

namespace Endatix.Core.Tests.Entities;

/// <summary>Submissions and assertions the lifecycle event tests share.</summary>
internal static class SubmissionLifecycleTestKit
{
    public const long TenantId = 1;
    public const long FormId = 100;
    public const long FormDefinitionId = 200;

    internal static readonly SubmissionCreateArgs Started = new(
        TenantId: TenantId,
        FormId: FormId,
        FormDefinitionId: FormDefinitionId,
        JsonData: "{}",
        IsComplete: false,
        StartSubmission: true);

    internal static Submission Create(SubmissionCreateArgs? args = null) => Submission.Create(args ?? Started);

    internal static Submission CreateCleared(bool startSubmission = true)
    {
        var submission = Create(Started with { StartSubmission = startSubmission });
        submission.ClearDomainEvents();
        return submission;
    }

    /// <summary>A submission as it loads from a row stored in <paramref name="code"/>.</summary>
    internal static Submission Loaded(string code, bool startSubmission = true)
    {
        var submission = CreateCleared(startSubmission);
        typeof(Submission).GetProperty(nameof(Submission.CollectionStatus), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(submission, CollectionStatus.FromCode(code));
        return submission;
    }

    internal static void SaveAnswers(Submission submission, string jsonData, bool isComplete = false) =>
        submission.Update(jsonData, FormDefinitionId, formDefinitionFormId: FormId, isComplete: isComplete);

    internal static SubmissionLifecycleEvent SingleLifecycleEvent(Submission submission) =>
        submission.DomainEvents.OfType<SubmissionLifecycleEvent>().Should().ContainSingle().Subject;

    internal static SubmissionLifecycleEvent AssertSingle(Submission submission, (string EventType, string Previous, string Next) expected)
    {
        var (eventType, previous, next) = expected;
        var lifecycle = SingleLifecycleEvent(submission);
        lifecycle.EventType.Should().Be(eventType);
        var payload = (SubmissionLifecyclePayload)lifecycle.GetPayload();
        payload.CollectionStatus.Code.Should().Be(next);
        payload.PreviousCollectionStatus!.Value.Code.Should().Be(previous);
        payload.Revision.Should().Be(submission.Revision);
        submission.DomainEvents.OfType<SubmissionUpdatedEvent>().Should().BeEmpty();
        return lifecycle;
    }
}
