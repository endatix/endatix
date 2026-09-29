using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public class SubmissionFlatteningProcessorTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const long SubmissionId = 500;

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IFlattenedSubmissionRepository _rows = Substitute.For<IFlattenedSubmissionRepository>();
    private readonly IFormSchemaProvider _schemas = Substitute.For<IFormSchemaProvider>();

    [Fact]
    public async Task ProcessAsync_CompletedSubmission_WritesFlattenedDataAtItsRevision()
    {
        // Arrange
        var submission = CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 4);
        GivenSubmission(submission);
        GivenSchema();
        GivenRowAcceptsRevision(true);
        _submissions.AnyAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).EnsureExistsAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>());
        await _rows.Received(1).TryMarkProcessingAsync(TenantId, SubmissionId, 4, Arg.Any<CancellationToken>());
        await _rows.Received(1).TryMarkProcessedAsync(
            TenantId, SubmissionId, 4, Arg.Is<string>(json => json.Contains("firstName")), Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().DeleteBySubmissionAsync(default, default, default, default);
    }

    [Fact]
    public async Task ProcessAsync_SubmissionGone_RemovesItsRowAndSucceeds()
    {
        // Arrange — deleted before this flatten, or before its retry.
        GivenSubmission(null);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — no row is created for it, and any it had is removed.
        await _rows.Received(1).DeleteBySubmissionAsync(TenantId, FormId, SubmissionId, Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().EnsureExistsAsync(default, default, default, default);
        await _schemas.DidNotReceiveWithAnyArgs().GetOrCompileAsync(default, default, default, default);
    }

    [Fact]
    public async Task ProcessAsync_SubmissionGoneAfterWrite_RemovesTheRowItWrote()
    {
        // Arrange — the submission is deleted, and its cleanup has run, while this flatten was running.
        GivenSubmission(CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 2));
        GivenSchema();
        GivenRowAcceptsRevision(true);
        _submissions.AnyAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).DeleteBySubmissionAsync(TenantId, FormId, SubmissionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RowWrittenFromNewerRevision_WritesNothing()
    {
        // Arrange — a flatten of a later revision already wrote the row.
        GivenSubmission(CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 2));
        GivenSchema();
        GivenRowAcceptsRevision(false);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — the stale flatten succeeds without writing data or flipping the row back.
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default, default, default!, default);
        await _schemas.DidNotReceiveWithAnyArgs().GetOrCompileAsync(default, default, default, default);
    }

    [Fact]
    public async Task ProcessAsync_NewerRevisionWrittenMeanwhile_LeavesTheNewerData()
    {
        // Arrange — the row accepts this revision as processing, but a newer flatten writes before this one does.
        GivenSubmission(CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 2));
        GivenSchema();
        _rows.TryMarkProcessingAsync(TenantId, SubmissionId, 2, Arg.Any<CancellationToken>()).Returns(true);
        _rows.TryMarkProcessedAsync(TenantId, SubmissionId, 2, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await _submissions.DidNotReceiveWithAnyArgs().AnyAsync(default(SubmissionWithDefinitionAndFormSpec)!, default);
        await _rows.DidNotReceiveWithAnyArgs().DeleteBySubmissionAsync(default, default, default, default);
    }

    [Fact]
    public async Task ProcessAsync_IncompleteSubmission_MarksSkippedAtItsRevision()
    {
        // Arrange
        var submission = Submission.Create(new SubmissionCreateArgs(TenantId, FormId, FormDefinitionId, "{}", IsComplete: false));
        submission.Id = SubmissionId;
        GivenSubmission(submission);
        GivenRowAcceptsRevision(true);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).TryMarkSkippedAsync(TenantId, SubmissionId, submission.Revision, Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default, default, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_UnavailableSchema_Throws()
    {
        // Arrange
        GivenSubmission(CompletedSubmission("{}", revision: 1));
        _schemas.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);
        GivenRowAcceptsRevision(true);

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*form {FormId}*definition {FormDefinitionId}*");
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default, default, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_TenantMismatch_MarksRowFailed()
    {
        // Arrange
        var submission = Submission.Create(new SubmissionCreateArgs(TenantId: 2, FormId, FormDefinitionId, "{}", IsComplete: true));
        submission.Id = SubmissionId;
        GivenSubmission(submission);
        GivenRowAcceptsRevision(true);

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).TryMarkFailedAsync(
            TenantId,
            SubmissionId,
            submission.Revision,
            "Submission tenant or form does not match the flatten request.",
            Arg.Any<CancellationToken>());
        await _schemas.DidNotReceiveWithAnyArgs().GetOrCompileAsync(default, default, default, default);
    }

    private SubmissionFlatteningProcessor Processor() =>
        new(_submissions, _rows, _schemas, NullLogger<SubmissionFlatteningProcessor>.Instance);

    private static Submission CompletedSubmission(string json, long revision)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(TenantId, FormId, FormDefinitionId, json, IsComplete: true));
        submission.Id = SubmissionId;
        while (submission.Revision < revision)
        {
            submission.IncrementRevision();
        }

        return submission;
    }

    private void GivenSubmission(Submission? submission) =>
        _submissions
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

    private void GivenSchema()
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(FormSchemaFixtureLoader.LoadText("simple-definition.json"));
        _schemas.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns(new FormSchemaEntity(TenantId, FormId, FormDefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson));
    }

    private void GivenRowAcceptsRevision(bool accepts)
    {
        _rows.TryMarkProcessingAsync(default, default, default, default).ReturnsForAnyArgs(accepts);
        _rows.TryMarkProcessedAsync(default, default, default, default!, default).ReturnsForAnyArgs(accepts);
        _rows.TryMarkSkippedAsync(default, default, default, default).ReturnsForAnyArgs(accepts);
        _rows.TryMarkFailedAsync(default, default, default, default!, default).ReturnsForAnyArgs(accepts);
    }
}
