using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.SetTest;

namespace Endatix.Core.Tests.UseCases.Submissions.SetTest;

public class SetSubmissionTestHandlerTests
{
    private const long TenantId = SampleData.TENANT_ID;
    private const long FormId = 10;
    private const long SubmissionId = 20;
    private const long SubmitterId = 30;
    private const long ExistingSubmissionId = 40;
    private const long LinkId = 50;

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly SetSubmissionTestHandler _handler;

    public SetSubmissionTestHandlerTests()
    {
        _handler = new SetSubmissionTestHandler(_submissions, _forms);
    }

    private static Form LimitedForm() =>
        Form.Create(FormId, new FormCreateArgs(
            TenantId: SampleData.TENANT_ID, Name: "Form", IsEnabled: true, IsPublic: false, LimitOnePerUser: true));

    private static Submission NewSubmission(bool isTest, long id = SubmissionId) =>
        Submission.Create(id, new SubmissionCreateArgs(
            TenantId: SampleData.TENANT_ID,
            FormId: FormId,
            FormDefinitionId: 1,
            JsonData: "{}",
            SubmitterId: SubmitterId,
            IsTestSubmission: isTest,
            EnforceSingleSubmissionGate: true));

    private void Given(Submission? submission, Form? form = null)
    {
        _submissions.FirstOrDefaultAsync(Arg.Any<SubmissionByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => submission is not null && call.Arg<SubmissionByIdForTenantSpec>().IsSatisfiedBy(submission)
                ? submission
                : null);
        GivenForm(form ?? LimitedForm());
    }

    private void GivenForm(Form? form) =>
        _forms.FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(call => form is not null && call.Arg<FormSpecifications.ByIdForTenant>().IsSatisfiedBy(form) ? form : null);

    [Fact]
    public async Task Handle_SubmissionMissing_ReturnsNotFound()
    {
        // Arrange
        Given(null);

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubmissionOnAnotherForm_ReturnsNotFound()
    {
        // Arrange
        Given(NewSubmission(isTest: false));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId + 1, SubmissionId, true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_NoTenant_IsUnauthorizedAndReadsNothing()
    {
        // Arrange
        Given(NewSubmission(isTest: false));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(0, FormId, SubmissionId, true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
        await _submissions.DidNotReceive().FirstOrDefaultAsync(Arg.Any<ISpecification<Submission>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubmissionOfAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        Given(NewSubmission(isTest: false));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId + 1, FormId, SubmissionId, true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FormMissing_ReturnsNotFound()
    {
        // Arrange
        Given(NewSubmission(isTest: false));
        GivenForm(null);

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, true), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_AlreadyInRequestedState_SucceedsWithoutSaving()
    {
        // Arrange
        Given(NewSubmission(isTest: true));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, true), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsTestSubmission.Should().BeTrue();
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsTest_SavesTestSubmissionWithoutRestrictionKey()
    {
        // Arrange
        var submission = NewSubmission(isTest: false);
        Given(submission);

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, true), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsTestSubmission.Should().BeTrue();
        submission.RestrictionKey.Should().BeNull();
        await _submissions.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsRealOnLimitedForm_RestoresRestrictionKey()
    {
        // Arrange
        var submission = NewSubmission(isTest: true);
        Given(submission);

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsTestSubmission.Should().BeFalse();
        submission.RestrictionKey.Should().Be($"SingleSubmission:Form:{FormId}:Submitter:{SubmitterId}");
    }

    [Fact]
    public async Task Handle_MarkAsRealWhenPersonHasRealSubmission_ReturnsConflictWithItsId()
    {
        // Arrange
        Given(NewSubmission(isTest: true));
        _submissions.FirstOrDefaultAsync(Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(NewSubmission(isTest: false, ExistingSubmissionId));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Existing.Should().Be(new ExistingResource("submission_exists", "submissionId", ExistingSubmissionId.ToString()));
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsRealWhenPersonHasRealSubmission_ReadsItOnce()
    {
        // Arrange
        Given(NewSubmission(isTest: true));
        _submissions.FirstOrDefaultAsync(Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(NewSubmission(isTest: false, ExistingSubmissionId));

        // Act
        await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        await _submissions.Received(1).FirstOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>());
        await _submissions.DidNotReceive().AnyAsync(Arg.Any<ISpecification<Submission>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsRealWhenItsLinkOpenedAnotherRealSubmission_ReturnsConflictWithItsId()
    {
        // Arrange
        var form = Form.Create(FormId, new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Form", IsEnabled: true));
        var submission = NewSubmission(isTest: true);
        submission.FreezePersonalization(LinkId, """{"source":"link"}""");
        Given(submission, form);
        _submissions.FirstOrDefaultAsync(
                Arg.Is<ISpecification<Submission>>(spec => !(spec is SubmissionByFormIdAndSubmitterIdSpec) && !(spec is SubmissionByIdForTenantSpec)),
                Arg.Any<CancellationToken>())
            .Returns(NewSubmission(isTest: false, ExistingSubmissionId));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Existing!.Id.Should().Be(ExistingSubmissionId.ToString());
        submission.IsTestSubmission.Should().BeTrue();
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsRealLosesRaceOnUniqueIndex_ReturnsConflictWithItsId()
    {
        // Arrange
        Given(NewSubmission(isTest: true));
        _submissions.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new DuplicateSubmissionException("duplicate"));
        // None at the fast path; the winner is there once the unique index refuses the save.
        _submissions.FirstOrDefaultAsync(Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((Submission?)null, NewSubmission(isTest: false, ExistingSubmissionId));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Existing!.Id.Should().Be(ExistingSubmissionId.ToString());
    }

    [Fact]
    public async Task Handle_MarkAsRealOnUnlimitedForm_SkipsConflictCheck()
    {
        // Arrange
        var form = Form.Create(FormId, new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Form", IsEnabled: true));
        var submission = NewSubmission(isTest: true);
        Given(submission, form);

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        submission.RestrictionKey.Should().BeNull();
        await _submissions.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<SubmissionByFormIdAndSubmitterIdSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsRealOnATestOpenedAsAPerson_IsInvalidAndKeepsItATest()
    {
        // Arrange
        var submission = NewSubmission(isTest: true);
        submission.FreezeForTest("""{"source":"test","memberId":7,"identifier":"panel-42","variables":{}}""");
        Given(submission);

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        submission.IsTestSubmission.Should().BeTrue();
        await _submissions.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MarkAsTestOfASubmissionDeletedMeanwhile_ReturnsNotFound()
    {
        // Arrange
        Given(NewSubmission(isTest: false));
        _submissions.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new SubmissionDeletedException("The submission was deleted while it was being saved."));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, true), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_MarkAsRealLosesADeadlock_ReturnsUnavailable()
    {
        // Arrange
        Given(NewSubmission(isTest: true));
        _submissions.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new DeadlockVictimException("deadlock"));

        // Act
        var result = await _handler.Handle(new SetSubmissionTestCommand(TenantId, FormId, SubmissionId, false), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
    }
}
