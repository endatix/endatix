using Ardalis.Specification;
using Endatix.Core.Abstractions.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.TestSubmissions;

namespace Endatix.Modules.Audience.Tests.Features.TestSubmissions;

public sealed class TestSubmissionWriterTests
{
    private const long CurrentDefinitionId = 77;

    private static readonly PreparedTestSubmission _prepared = new(
        new TestSubject(TenantId: 1, FormId: 10, MembershipId: 30, MemberId: 40, Identifier: "ada@example.com"),
        [],
        new Token(24),
        DateTime.UnixEpoch);

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IRepository<Form> _forms = Substitute.For<IRepository<Form>>();
    private readonly IFormSubmissionsLock _formLock = Substitute.For<IFormSubmissionsLock>();
    private readonly IFormSubmissionsSection _section = Substitute.For<IFormSubmissionsSection>();

    public TestSubmissionWriterTests()
    {
        _formLock.BeginAddingAsync(10, Arg.Any<CancellationToken>())
            .Returns(Result<IFormSubmissionsSection>.Success(_section));
        FormReadsOpen(CurrentDefinitionId);
    }

    [Fact]
    public async Task AddAsync_FormOpen_InsertsOnTheDefinitionReadUnderTheLockAndCommits()
    {
        // Act
        var result = await Writer().AddAsync(_prepared, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Token.Should().Be(_prepared.Token.Value);
        await _submissions.Received(1).AddAsync(
            Arg.Is<Submission>(submission => submission.FormDefinitionId == CurrentDefinitionId),
            Arg.Any<CancellationToken>());
        await _section.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_FormBeingDeleted_IsUnavailableAndReadsNothing()
    {
        // Arrange
        _formLock.BeginAddingAsync(10, Arg.Any<CancellationToken>())
            .Returns(Result<IFormSubmissionsSection>.Unavailable("The form is busy; try again shortly."));

        // Act
        var result = await Writer().AddAsync(_prepared, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _forms.DidNotReceive().FirstOrDefaultAsync(
            Arg.Any<ISpecification<Form, long?>>(),
            Arg.Any<CancellationToken>());
        await _submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_FormNoLongerOpen_IsNotFoundAndKeepsTheCurrentSubmission()
    {
        // Arrange
        FormReadsOpen(null);
        var current = _prepared.Build(CurrentDefinitionId);

        // Act
        var result = await Writer().ReplaceAsync(current, _prepared, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        current.IsDeleted.Should().BeFalse();
        await _submissions.DidNotReceive().AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>());
        await _section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_FormOpen_DeletesTheCurrentSubmissionInTheSameSave()
    {
        // Arrange
        var current = _prepared.Build(CurrentDefinitionId);
        var deletedAtSave = false;
        _submissions.AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                deletedAtSave = current.IsDeleted;
                return call.Arg<Submission>();
            });

        // Act
        var result = await Writer().ReplaceAsync(current, _prepared, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Created);
        deletedAtSave.Should().BeTrue();
        await _section.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_CurrentDeletedMeanwhile_IsNotFoundAndCommitsNothing()
    {
        // Arrange
        SaveThrows(new SubmissionDeletedException("The submission was deleted while it was being saved."));

        // Act
        var result = await Writer().ReplaceAsync(
            _prepared.Build(CurrentDefinitionId),
            _prepared,
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_SaveLosesADeadlock_IsUnavailableAndCommitsNothing()
    {
        // Arrange
        SaveThrows(new DeadlockVictimException("deadlock"));

        // Act
        var result = await Writer().AddAsync(_prepared, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
        await _section.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    private TestSubmissionWriter Writer() => new(_submissions, _forms, _formLock);

    private void FormReadsOpen(long? definitionId) =>
        _forms.FirstOrDefaultAsync(Arg.Any<ISpecification<Form, long?>>(), Arg.Any<CancellationToken>())
            .Returns(definitionId);

    private void SaveThrows(Exception exception) =>
        _submissions.AddAsync(Arg.Any<Submission>(), Arg.Any<CancellationToken>())
            .Returns<Task<Submission>>(_ => throw exception);
}
