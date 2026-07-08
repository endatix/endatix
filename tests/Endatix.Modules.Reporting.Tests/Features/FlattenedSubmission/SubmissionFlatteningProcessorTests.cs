using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
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
    public async Task ProcessAsync_WithCompletedSubmission_FlattensIntoTrackingRow()
    {
        string definitionJson = File.ReadAllText(GetFixturePath("simple-definition.json"));
        string submissionJson = File.ReadAllText(GetFixturePath("simple-submission.json"));
        FormSchemaCompiler compiler = new();
        MergedFormSchema mergedSchema = compiler.CompileFromPersistedSchema(definitionJson, existingSchemaJson: null);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionId, mergedSchema.ToJson());

        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId,
            FormId,
            FormDefinitionId,
            submissionJson,
            IsComplete: true));
        submission.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        IFormSchemaProvider schemaProvider = Substitute.For<IFormSchemaProvider>();
        schemaProvider
            .GetOrCompileAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IFlattenedSubmissionRepository flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(SubmissionId, TenantId, FormId, Arg.Any<CancellationToken>())
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
    public async Task ProcessAsync_WithTenantMismatch_MarksRowFailed()
    {
        Submission submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: 2,
            FormId,
            FormDefinitionId,
            JsonData: "{}",
            IsComplete: true));
        submission.Id = SubmissionId;

        FlattenedSubmissionRow trackingRow = new(SubmissionId, TenantId, FormId);

        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);

        IFormSchemaProvider schemaProvider = Substitute.For<IFormSchemaProvider>();

        IFlattenedSubmissionRepository flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
        flattenedSubmissionRepository
            .GetOrCreateAsync(SubmissionId, TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(trackingRow);

        SubmissionFlatteningProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, SubmissionId, TestContext.Current.CancellationToken);

        trackingRow.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Failed);
        trackingRow.Integration.LastError.Should().Be("Submission not found.");
        await schemaProvider.DidNotReceive().GetOrCompileAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    private static string GetFixturePath(string fixtureName) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Features",
            "FormSchema",
            "FlattenedFormDefinition",
            "Fixtures",
            fixtureName);
}
