using System.Reflection;
using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class CollectionStatusTests
{
    [Fact]
    public void FromCode_UnknownCode_DoesNotThrow()
    {
        var status = CollectionStatus.FromCode("panel_hold");

        Assert.Equal("panel_hold", status.Code);
        Assert.Equal("panel_hold", status.Name);
    }

    [Fact]
    public void FromCode_BuiltIn_UsesCatalogLabel()
    {
        var status = CollectionStatus.FromCode("SCREEN_OUT");

        Assert.Equal(CollectionStatusCodes.ScreenOut, status.Code);
        Assert.Equal("Screened out", status.Name);
    }

    [Theory]
    [InlineData(true, CollectionStatusCodes.Complete)]
    [InlineData(false, CollectionStatusCodes.NotStarted)]
    public void Create_DualWritesCollectionStatus(bool isComplete, string expected)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: isComplete));

        Assert.Equal(isComplete, submission.IsComplete);
        Assert.Equal(expected, submission.CollectionStatus.Code);
    }

    [Fact]
    public void Cancel_DoesNotMarkComplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: false));

        submission.Cancel();

        Assert.False(submission.IsComplete);
        Assert.Equal(CollectionStatusCodes.Cancelled, submission.CollectionStatus.Code);
    }

    [Fact]
    public void Cancel_WhenComplete_ThrowsAndKeepsComplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: true));

        var cancel = () => submission.Cancel();

        Assert.Throws<InvalidOperationException>(cancel);
        Assert.True(submission.IsComplete);
        Assert.Equal(CollectionStatusCodes.Complete, submission.CollectionStatus.Code);
    }

    [Fact]
    public void Update_AfterCancel_DoesNotComplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: false));
        submission.Cancel();

        var complete = () => submission.Update("{\"a\":1}", 3, 2, isComplete: true);

        Assert.Throws<InvalidOperationException>(complete);
        Assert.False(submission.IsComplete);
        Assert.Equal("{}", submission.JsonData);
        Assert.Equal(CollectionStatusCodes.Cancelled, submission.CollectionStatus.Code);
    }

    [Fact]
    public void Create_WhenRespondentStarts_IsInProgress()
    {
        var submission = OpenSubmission(start: true);

        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
        Assert.NotNull(submission.StartedAt);
    }

    [Fact]
    public void Update_FromNotStarted_BecomesInProgress()
    {
        var submission = OpenSubmission(start: false);

        submission.Update("{\"a\":1}", formDefinitionId: 3, formDefinitionFormId: 2, isComplete: false);

        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
        Assert.NotNull(submission.StartedAt);
        Assert.False(submission.IsComplete);
    }

    [Fact]
    public void Update_FromViewed_BecomesInProgress()
    {
        var submission = OpenSubmission(start: false);
        typeof(Submission).GetProperty(nameof(Submission.CollectionStatus))!
            .SetValue(submission, CollectionStatus.Viewed);

        submission.Update("{\"a\":1}", formDefinitionId: 3, formDefinitionFormId: 2, isComplete: false);

        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
        Assert.NotNull(submission.StartedAt);
    }

    [Fact]
    public void Create_NotStarted_HasNoStartAndIsResumable()
    {
        var submission = OpenSubmission(start: false);

        Assert.Equal(CollectionStatusCodes.NotStarted, submission.CollectionStatus.Code);
        Assert.Equal("Not started", submission.CollectionStatus.Name);
        Assert.Null(submission.StartedAt);
        Assert.Null(submission.CompletedAt);
        Assert.False(submission.IsComplete);
    }

    [Fact]
    public void Create_CompleteOnBehalf_IsCompleteNotNotStarted()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: true,
            StartSubmission: false));

        Assert.Equal(CollectionStatusCodes.Complete, submission.CollectionStatus.Code);
        Assert.NotNull(submission.StartedAt);
        Assert.Equal(submission.CompletedAt, submission.StartedAt);
    }

    [Fact]
    public void Create_CompleteByRespondent_IsComplete()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: true,
            StartSubmission: true));

        Assert.Equal(CollectionStatusCodes.Complete, submission.CollectionStatus.Code);
        Assert.True(submission.IsComplete);
        Assert.NotNull(submission.StartedAt);
    }

    [Fact]
    public void ObsoleteCtor_Incomplete_IsNotStarted()
    {
#pragma warning disable CS0618
        var submission = new Submission(1, "{}", formId: 2, formDefinitionId: 3, isComplete: false);
#pragma warning restore CS0618

        Assert.Equal(CollectionStatusCodes.NotStarted, submission.CollectionStatus.Code);
        Assert.Null(submission.StartedAt);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.NotStarted)]
    [InlineData(CollectionStatusCodes.Viewed)]
    public void Update_CompleteFromUnengaged_BecomesComplete(string from)
    {
        var submission = SubmissionWithStatus(from);

        submission.Update("{\"a\":1}", formDefinitionId: 3, formDefinitionFormId: 2, isComplete: true);

        Assert.True(submission.IsComplete);
        Assert.Equal(CollectionStatusCodes.Complete, submission.CollectionStatus.Code);
        Assert.NotNull(submission.StartedAt);
        Assert.NotNull(submission.CompletedAt);
        Assert.True(submission.StartedAt <= submission.CompletedAt);
    }

    [Fact]
    public void Update_SecondSaveOfStartedRow_KeepsFirstStartedAt()
    {
        var submission = OpenSubmission(start: false);
        submission.Update("{\"a\":1}", 3, 2, isComplete: false);
        var firstStart = submission.StartedAt;

        submission.Update("{\"a\":2}", 3, 2, isComplete: false);

        Assert.Equal(firstStart, submission.StartedAt);
        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.NotStarted)]
    [InlineData(CollectionStatusCodes.Viewed)]
    public void EnsureStarted_FromUnengaged_BecomesInProgress(string from)
    {
        var submission = SubmissionWithStatus(from);
        var at = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        submission.EnsureStarted(at);

        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
        Assert.Equal(at, submission.StartedAt);
    }

    [Fact]
    public void EnsureStarted_UnengagedRowWithStartedAt_HealsToInProgress()
    {
        var submission = SubmissionWithStatus(CollectionStatusCodes.NotStarted);
        var at = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        typeof(Submission).GetProperty(nameof(Submission.StartedAt))!.SetValue(submission, at);

        submission.EnsureStarted();

        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
        Assert.Equal(at, submission.StartedAt);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.Cancelled)]
    [InlineData(CollectionStatusCodes.ScreenOut)]
    [InlineData(CollectionStatusCodes.QuotaFull)]
    [InlineData(CollectionStatusCodes.Abandoned)]
    [InlineData(CollectionStatusCodes.Expired)]
    [InlineData("panel_hold")]
    public void EnsureStarted_OnNonUnengagedStatus_DoesNotChangeStatus(string from)
    {
        var submission = SubmissionWithStatus(from);

        submission.EnsureStarted();

        Assert.Equal(from, submission.CollectionStatus.Code);
    }

    [Fact]
    public void EnsureStarted_OnComplete_StaysComplete()
    {
        var submission = OpenSubmission(start: false);
        submission.Update("{}", 3, 2, isComplete: true);

        submission.EnsureStarted();

        Assert.Equal(CollectionStatusCodes.Complete, submission.CollectionStatus.Code);
        Assert.True(submission.IsComplete);
    }

    [Fact]
    public void Cancel_FromNotStarted_IsCancelledAndCannotComplete()
    {
        var submission = OpenSubmission(start: false);

        submission.Cancel();
        var complete = () => submission.Update("{\"a\":1}", 3, 2, isComplete: true);

        Assert.Equal(CollectionStatusCodes.Cancelled, submission.CollectionStatus.Code);
        Assert.Throws<InvalidOperationException>(complete);
        Assert.False(submission.IsComplete);
        Assert.Null(submission.CompletedAt);
        Assert.Equal(CollectionStatusCodes.Cancelled, submission.CollectionStatus.Code);
    }

    [Fact]
    public void Update_IncompleteAfterCancel_StaysCancelled()
    {
        var submission = OpenSubmission(start: false);
        submission.Cancel();

        submission.Update("{\"a\":1}", 3, 2, isComplete: false);

        Assert.Equal(CollectionStatusCodes.Cancelled, submission.CollectionStatus.Code);
        Assert.False(submission.IsComplete);
    }

    [Fact]
    public void Update_FromExpired_BecomesInProgress()
    {
        var submission = SubmissionWithStatus(CollectionStatusCodes.Expired);

        submission.Update("{\"a\":1}", 3, 2, isComplete: false);

        Assert.Equal(CollectionStatusCodes.InProgress, submission.CollectionStatus.Code);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.ScreenOut)]
    [InlineData(CollectionStatusCodes.QuotaFull)]
    [InlineData(CollectionStatusCodes.Abandoned)]
    [InlineData("panel_hold")]
    public void Update_CompleteFromNonResumable_Throws(string from)
    {
        var submission = SubmissionWithStatus(from);

        var complete = () => submission.Update("{\"a\":1}", 3, 2, isComplete: true);

        Assert.Throws<InvalidOperationException>(complete);
        Assert.False(submission.IsComplete);
        Assert.Equal(from, submission.CollectionStatus.Code);
    }

    [Theory]
    [InlineData(CollectionStatusCodes.NotStarted, "Not started")]
    [InlineData(CollectionStatusCodes.Viewed, "Viewed")]
    [InlineData(" NOT_STARTED ", "Not started")]
    public void FromCode_NewBuiltIns_UseCatalogLabel(string code, string expectedName)
    {
        var status = CollectionStatus.FromCode(code);

        Assert.Equal(code.Trim().ToLowerInvariant(), status.Code);
        Assert.Equal(expectedName, status.Name);
    }

    [Fact]
    public void FromCode_ReturnsCopy_NotSharedCatalogInstance()
    {
        var status = CollectionStatus.FromCode(CollectionStatusCodes.NotStarted);

        Assert.False(ReferenceEquals(CollectionStatus.NotStarted, status));
        Assert.Equal(CollectionStatus.NotStarted, status);
    }

    private static Submission SubmissionWithStatus(string code)
    {
        var submission = OpenSubmission(start: false);
        typeof(Submission).GetProperty(nameof(Submission.CollectionStatus))!
            .SetValue(submission, CollectionStatus.FromCode(code));
        return submission;
    }

    private static Submission OpenSubmission(bool start) =>
        Submission.Create(new SubmissionCreateArgs(
            TenantId: 1,
            FormId: 2,
            FormDefinitionId: 3,
            JsonData: "{}",
            IsComplete: false,
            StartSubmission: start));
}
