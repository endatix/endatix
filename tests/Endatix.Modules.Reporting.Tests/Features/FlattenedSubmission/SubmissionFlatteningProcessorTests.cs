using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Data;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using FlattenedSubmissionKey = Endatix.Modules.Reporting.Domain.FlattenedSubmissionKey;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.FlattenedSubmission;

public partial class SubmissionFlatteningProcessorTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const long SubmissionId = 500;
    private static readonly FlattenedSubmissionKey Key = new(TenantId, FormId, SubmissionId);

    private readonly IRepository<Submission> _submissions = Substitute.For<IRepository<Submission>>();
    private readonly IFlattenedSubmissionRepository _rows = Substitute.For<IFlattenedSubmissionRepository>();
    private readonly IFormSchemaProvider _schemas = Substitute.For<IFormSchemaProvider>();
    private readonly IFormSchemaPersonalization _personalization = PassThroughPersonalization();

    private static IFormSchemaPersonalization PassThroughPersonalization()
    {
        var personalization = Substitute.For<IFormSchemaPersonalization>();
        personalization
            .IncludeAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<PersonalizationRead?>(), Arg.Any<CancellationToken>())
            .Returns(call => FormSchemaFlatteningMap.FromJson(call.Arg<FormSchemaEntity>().FlatteningMap));
        return personalization;
    }

    [Fact]
    public async Task ProcessAsync_CompletedSubmission_WritesFlattenedDataAtItsRevision()
    {
        // Arrange
        var submission = CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 4);
        GivenSubmission(submission);
        GivenSchema();
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).EnsureExistsAsync(new FlattenedSubmissionKey(TenantId, FormId, SubmissionId), Arg.Any<CancellationToken>());
        FlattenedRevision write = new(TenantId, SubmissionId, 4, submission.ModifiedAt ?? submission.CreatedAt);
        await _rows.Received(1).TryMarkProcessingAsync(write, Arg.Any<CancellationToken>());
        await _rows.Received(1).TryMarkProcessedAsync(
            write,
            Arg.Is<FlattenedRowData>(data => data.DataJson.Contains("firstName")),
            Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().DeleteBySubmissionAsync(default, default);
    }

    [Fact]
    public async Task ProcessAsync_SchemaWithSearchKeys_WritesTheSearchCopyOfThoseKeys()
    {
        // Arrange
        GivenSubmission(CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 1));
        GivenSchema("""[{"key":"firstName","source":"auto","filterType":"text","addedAt":"2026-10-19T00:00:00Z"}]""");
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _rows.Received(1).TryMarkProcessedAsync(
            Arg.Any<FlattenedRevision>(),
            Arg.Is<FlattenedRowData>(data => data.SearchJson == """{"firstName":"ada"}"""),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_SubmissionWithSnapshot_WritesTheFrozenValuesBesideTheAnswers()
    {
        // Arrange
        var submission = CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 1);
        submission.FreezeOnBehalf("""{"identifier":"CG-1","variables":{"city":"Columbus"}}""");
        GivenSubmission(submission);
        GivenSchema();
        GivenRowAcceptsRevision(true);
        _personalization
            .IncludeAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<PersonalizationRead?>(), Arg.Any<CancellationToken>())
            .Returns(call => WithAudienceColumns(call.Arg<FormSchemaEntity>(), call.Arg<PersonalizationRead?>()));
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await _personalization.Received(1).IncludeAsync(
            Arg.Any<FormSchemaEntity>(),
            Arg.Is<PersonalizationRead?>(read => read != null && read.Identifier == "CG-1"),
            Arg.Any<CancellationToken>());
        await _rows.Received(1).TryMarkProcessedAsync(
            Arg.Any<FlattenedRevision>(),
            Arg.Is<FlattenedRowData>(data =>
                data.DataJson.Contains("firstName") &&
                data.DataJson.Contains("\"AudienceIdentifier\":\"CG-1\"") &&
                data.DataJson.Contains("\"audience_city\":\"Columbus\"")),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProcessAsync_SubmissionOrItsFormSoftDeleted_RemovesItsRowAndSucceeds(
        bool submissionDeleted,
        bool formDeleted)
    {
        // Arrange — soft-deleted before this flatten, or before its retry.
        GivenMissingSubmission(new SubmissionDeletionState(FormId, submissionDeleted, formDeleted));

        // Act
        await Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — the row is removed as the deletion syncs remove it, never created or flattened.
        await _rows.Received(1).DeleteBySubmissionAsync(Key, Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().EnsureExistsAsync(default, default);
        await _rows.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await _schemas.DidNotReceiveWithAnyArgs().GetOrCompileAsync(default, default, default, default);
    }

    [Theory]
    [InlineData("never existed, or another tenant's")]
    [InlineData("deleted in another form")]
    [InlineData("in another form that was deleted")]
    [InlineData("neither it nor its form deleted, but not readable here")]
    public async Task ProcessAsync_SubmissionNotDeletedInThisForm_ThrowsWithoutTouchingRows(string situation)
    {
        // Arrange
        SubmissionDeletionState? state = situation switch
        {
            "never existed, or another tenant's" => null,
            "deleted in another form" => new SubmissionDeletionState(FormId: 999, IsDeleted: true, FormIsDeleted: false),
            "in another form that was deleted" => new SubmissionDeletionState(FormId: 999, IsDeleted: false, FormIsDeleted: true),
            _ => new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false),
        };
        GivenMissingSubmission(state);

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert — the job retries and then dead-letters; the read model is left as it was.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");
        await _rows.DidNotReceiveWithAnyArgs().EnsureExistsAsync(default, default);
        await _rows.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await _rows.DidNotReceiveWithAnyArgs().DeleteBySubmissionAsync(default, default);
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
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default!, default);
        await _schemas.DidNotReceiveWithAnyArgs().GetOrCompileAsync(default, default, default, default);
        await _submissions.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_NewerRevisionWrittenMeanwhile_LeavesTheNewerData()
    {
        // Arrange — the row accepts this revision as processing, but a newer flatten writes before this one does.
        var submission = CompletedSubmission(FormSchemaFixtureLoader.LoadText("simple-submission.json"), revision: 2);
        GivenSubmission(submission);
        GivenSchema();
        _rows.TryMarkProcessingAsync(WriteOf(submission), Arg.Any<CancellationToken>()).Returns(true);
        _rows.TryMarkProcessedAsync(WriteOf(submission), Arg.Any<FlattenedRowData>(), Arg.Any<CancellationToken>()).Returns(false);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await _submissions.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().DeleteBySubmissionAsync(default, default);
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
        await _rows.Received(1).TryMarkSkippedAsync(WriteOf(submission), Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default!, default);
    }

    [Fact]
    public async Task ProcessAsync_IncompleteSubmissionWithIncludeIncomplete_WritesFlattenedDataAtItsRevision()
    {
        // Arrange — a draft, as the incomplete backfill scope asks for it.
        var draft = Submission.Create(new SubmissionCreateArgs(
            TenantId, FormId, FormDefinitionId, FormSchemaFixtureLoader.LoadText("simple-submission.json"), IsComplete: false));
        draft.Id = SubmissionId;
        GivenSubmission(draft);
        GivenSchema();
        GivenRowAcceptsRevision(true);
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));

        // Act
        await Processor().ProcessAsync(
            TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken, includeIncomplete: true);

        // Assert
        await _rows.Received(1).TryMarkProcessedAsync(
            WriteOf(draft),
            Arg.Is<FlattenedRowData>(data => data.DataJson.Contains("firstName")),
            Arg.Any<CancellationToken>());
        await _rows.DidNotReceiveWithAnyArgs().TryMarkSkippedAsync(default, default);
    }

    [Fact]
    public async Task ProcessAsync_UnavailableSchema_Throws()
    {
        // Arrange — neither the submission nor its form is deleted.
        GivenSubmission(CompletedSubmission("{}", revision: 1));
        GivenDeletionState(new SubmissionDeletionState(FormId, IsDeleted: false, FormIsDeleted: false));
        GivenNoSchema();
        GivenRowAcceptsRevision(true);

        // Act
        var act = () => Processor().ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*form {FormId}*definition {FormDefinitionId}*");
        await _rows.DidNotReceiveWithAnyArgs().TryMarkProcessedAsync(default, default!, default);
        await _rows.DidNotReceiveWithAnyArgs().DeleteBySubmissionAsync(default, default);
        await _submissions.Received(1).SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
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
            WriteOf(submission),
            "Submission tenant or form does not match the flatten request.",
            Arg.Any<CancellationToken>());
        await _schemas.DidNotReceiveWithAnyArgs().GetOrCompileAsync(default, default, default, default);
        await _submissions.DidNotReceive().SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>());
    }

    private SubmissionFlatteningProcessor Processor() =>
        new(_submissions, _rows, _schemas, _personalization, NullLogger<SubmissionFlatteningProcessor>.Instance);

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

    private static FlattenedRevision WriteOf(Submission submission) =>
        new(TenantId, SubmissionId, submission.Revision, submission.ModifiedAt ?? submission.CreatedAt);

    private void GivenSubmission(Submission? submission) =>
        _submissions
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

    private void GivenMissingSubmission(SubmissionDeletionState? state)
    {
        GivenSubmission(null);
        GivenDeletionState(state);
    }

    private static MergedFormSchema WithAudienceColumns(FormSchemaEntity schema, PersonalizationRead? personalization) =>
        new FormSchemaCompiler().IncludePersonalization(
            FormSchemaFixtureLoader.LoadText("simple-definition.json"),
            new PersistedSchemaJson(schema.FlatteningMap, schema.Codebook),
            PersonalizationColumns.From(personalization)).FlatteningMap;

    private void GivenDeletionState(SubmissionDeletionState? state) =>
        _submissions.SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>()).Returns(state);

    private void GivenNoSchema() =>
        _schemas.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

    private void GivenSchema(string searchKeysJson = FormSchemaEntity.EmptySearchKeysJson)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(FormSchemaFixtureLoader.LoadText("simple-definition.json"));
        FormSchemaEntity schema = new(TenantId, FormId, FormDefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson);
        schema.UpdateSearchKeys(searchKeysJson, revision: 0);
        _schemas.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>()).Returns(schema);
    }

    private void GivenRowAcceptsRevision(bool accepts)
    {
        _rows.TryMarkProcessingAsync(default, default).ReturnsForAnyArgs(accepts);
        _rows.TryMarkProcessedAsync(default, default!, default).ReturnsForAnyArgs(accepts);
        _rows.TryMarkSkippedAsync(default, default).ReturnsForAnyArgs(accepts);
        _rows.TryMarkFailedAsync(default, default!, default).ReturnsForAnyArgs(accepts);
    }
}
