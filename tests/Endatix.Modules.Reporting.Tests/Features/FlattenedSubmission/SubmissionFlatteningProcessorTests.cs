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

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WithCompletedSubmission_FlattensIntoTrackingRow()
    {
        var definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        var submissionJson = FormSchemaFixtureLoader.LoadText("simple-submission.json");
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson);

        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            submissionJson,
            IsComplete: true));
        submission.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        var schemaProvider = Substitute.For<IFormSchemaProvider>();
        schemaProvider
            .GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns(schema);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);

        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        trackingRow.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        trackingRow.DataJson.Should().Contain("firstName");
        await flattenedSubmissionRepository.Received()
            .SaveAsync(trackingRow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WithIncompleteAndIncludeIncomplete_FlattensSameRow()
    {
        var definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        var submissionJson = FormSchemaFixtureLoader.LoadText("simple-submission.json");
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson);

        var draft = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            submissionJson,
            IsComplete: false));
        draft.Id = SubmissionId;

        var completed = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            """{"firstName":"Ada"}""",
            IsComplete: true));
        completed.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(draft, completed);

        var schemaProvider = Substitute.For<IFormSchemaProvider>();
        schemaProvider
            .GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns(schema);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);

        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        await processor.ProcessAsync(
            TenantId,
            FormId,
            SubmissionId,
            TestContext.Current.CancellationToken,
            includeIncomplete: true);
        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        trackingRow.SubmissionId.Should().Be(SubmissionId);
        trackingRow.IsDeleted.Should().BeFalse();
        trackingRow.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        trackingRow.DataJson.Should().Contain("Ada");
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WithIncompleteAndDefaultFlag_MarksSkipped()
    {
        var draft = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            JsonData: "{}",
            IsComplete: false));
        draft.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(draft);

        var schemaProvider = Substitute.For<IFormSchemaProvider>();
        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);

        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        trackingRow.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Skipped);
        trackingRow.DataJson.Should().BeNull();
        await schemaProvider.DidNotReceive().GetOrCompileAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WhenSubmissionWasSoftDeleted_MarksExistingRowDeleted()
    {
        // Arrange
        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);
        trackingRow.MarkProcessed("""{"q1":"a"}""", DateTime.UtcNow);
        (var processor, var flattenedSubmissionRepository, var schemaProvider) =
            CreateProcessorForMissingSubmission(
                new SubmissionDeletionState(TenantId, FormId, IsDeleted: true),
                trackingRow);

        // Act
        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        trackingRow.IsDeleted.Should().BeTrue();
        trackingRow.Integration.Code.Should().Be(
            SubmissionIntegrationStatusCodes.Processed,
            "the row must not be left in Processing");
        await flattenedSubmissionRepository.DidNotReceive()
            .GetOrCreateAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await flattenedSubmissionRepository.Received(1).SaveAsync(trackingRow, Arg.Any<CancellationToken>());
        await schemaProvider.DidNotReceive().GetOrCompileAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WhenSoftDeletedWithoutRow_CreatesNothing()
    {
        // Arrange
        (var processor, var flattenedSubmissionRepository, _) =
            CreateProcessorForMissingSubmission(
                new SubmissionDeletionState(TenantId, FormId, IsDeleted: true),
                existingRow: null);

        // Act
        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert
        await flattenedSubmissionRepository.DidNotReceive()
            .GetOrCreateAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await flattenedSubmissionRepository.DidNotReceive()
            .SaveAsync(Arg.Any<FlattenedSubmissionRow>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("never existed")]
    [InlineData("deleted on another form")]
    [InlineData("deleted in another tenant")]
    [InlineData("exists but filtered out")]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WhenSubmissionNotFoundForThisForm_ThrowsWithoutTouchingRows(
        string situation)
    {
        var state = situation switch
        {
            "never existed" => null,
            "deleted on another form" => new SubmissionDeletionState(TenantId, FormId: 999, IsDeleted: true),
            "deleted in another tenant" => new SubmissionDeletionState(TenantId: 2, FormId, IsDeleted: true),
            _ => new SubmissionDeletionState(TenantId, FormId, IsDeleted: false),
        };

        // Arrange
        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);
        (var processor, var flattenedSubmissionRepository, _) =
            CreateProcessorForMissingSubmission(state, trackingRow);

        // Act
        Func<Task> act = () => processor.ProcessAsync(
            TenantId,
            FormId,
            SubmissionId,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*was not found*");
        trackingRow.IsDeleted.Should().BeFalse();
        await flattenedSubmissionRepository.DidNotReceive()
            .GetOrCreateAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await flattenedSubmissionRepository.DidNotReceive()
            .SaveAsync(Arg.Any<FlattenedSubmissionRow>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_StoresTheSubmissionStampOfTheFlattenedVersion()
    {
        // Arrange
        var compiled = new FormSchemaCompiler().CompilePersisted(
            FormSchemaFixtureLoader.LoadText("simple-definition.json"));
        FormSchemaEntity schema = new(TenantId, FormId, FormDefinitionId, compiled.FlatteningMapJson, compiled.CodebookJson);
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            FormSchemaFixtureLoader.LoadText("simple-submission.json"),
            IsComplete: true));
        submission.Id = SubmissionId;
        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        var schemaProvider = Substitute.For<IFormSchemaProvider>();
        schemaProvider
            .GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns(schema);
        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);
        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        // Act
        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        // Assert: the submission's own stamp, not the worker's clock.
        trackingRow.SourceModifiedAt.Should().Be(submission.ModifiedAt ?? submission.CreatedAt);
    }

    private static (SubmissionFlatteningProcessor Processor, IFlattenedSubmissionRepository FlattenedRepository, IFormSchemaProvider SchemaProvider)
        CreateProcessorForMissingSubmission(SubmissionDeletionState? state, FlattenedSubmissionRow? existingRow)
    {
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns((Submission?)null);
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionDeletionStateSpec>(), Arg.Any<CancellationToken>())
            .Returns(state);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetBySubmissionIdAsync(TenantId, SubmissionId, Arg.Any<CancellationToken>())
            .Returns(existingRow);

        var schemaProvider = Substitute.For<IFormSchemaProvider>();
        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);
        return (processor, flattenedSubmissionRepository, schemaProvider);
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WithUnavailableSchema_Throws()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        var schemaProvider = Substitute.For<IFormSchemaProvider>();
        schemaProvider
            .GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);

        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        Func<Task> act = () => processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*form {FormId}*definition {FormDefinitionId}*");
        trackingRow.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processing);
    }

    [Fact]
    public async Task SubmissionFlatteningProcessor_ProcessAsync_WithTenantMismatch_MarksRowFailed()
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 2,
            FormId,
            FormDefinitionId,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        var schemaProvider = Substitute.For<IFormSchemaProvider>();

        var flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(TenantId, SubmissionId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);

        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        trackingRow.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Failed);
        trackingRow.Integration.LastError.Should().Be("Submission tenant or form does not match the flatten request.");
        await schemaProvider.DidNotReceive().GetOrCompileAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

}
