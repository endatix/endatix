using System.Text.Json;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class SubmissionBackfillProcessorIntegrationTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const long SubmissionId = 500;
    // In the form PostgreSQL returns a jsonb value, which is how the conditional write stores it.
    private const string ProcessedDataJson = """{"firstName": "Ada"}""";
    private const string SimpleDefinitionJson = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";
    private const string SimpleSubmissionJson = """{"q1":"hello"}""";

    private readonly DbIntegrationFixture _fixture;

    public SubmissionBackfillProcessorIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BackfillFormAsync_WithProcessedRow_IsIdempotentSkip()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var flattenedSubmissionRepository = CreateRepository(dbContext);
        await new FlattenedRowSeed(dbContext).ProcessedAsync(
            new FlattenedSubmissionKey(TenantId, FormId, SubmissionId),
            ProcessedDataJson,
            cancellationToken);

        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), cancellationToken)
            .Returns([new SubmissionBackfillCandidate(SubmissionId, null, DateTime.UtcNow.AddDays(-1))]);

        var flatteningProcessor = Substitute.For<ISubmissionFlatteningProcessor>();
        SubmissionBackfillProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor,
            NullLogger<SubmissionBackfillProcessor>.Instance);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        result.Skipped.Should().Be(1);
        result.Processed.Should().Be(0);
        await flatteningProcessor.DidNotReceive()
            .ProcessAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());

        var persisted = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        persisted.DataJson.Should().Be(ProcessedDataJson);
    }

    [Fact]
    public async Task BackfillFormAsync_WithUnflattenedRow_ProcessesAndPersistsFlatData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var flattenedSubmissionRepository = CreateRepository(dbContext);
        var formSchemaRepository = CreateSchemaRepository(dbContext);
        await SeedSimpleFormSchemaAsync(formSchemaRepository, cancellationToken);

        var submission = CreateSubmission(SimpleSubmissionJson, isComplete: true);
        var submissionRepository = CreateSubmissionRepository(submission);

        var processor = CreateBackfillProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            formSchemaRepository);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        result.Processed.Should().Be(1);
        result.Skipped.Should().Be(0);
        result.Failed.Should().Be(0);

        var persisted = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        persisted.DataJson.Should().NotBeNullOrWhiteSpace();

        using var actualDocument = JsonDocument.Parse(persisted.DataJson!);
        actualDocument.RootElement.GetProperty("q1").GetString().Should().Be("hello");
    }

    [Fact]
    public async Task BackfillFormAsync_WithAllQuestionsSubmission_ProducesGoldenFlatOutput()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        var definitionJson = AllQuestionsReportingFixtureLoader.LoadDefinitionText();
        var submissionJson = AllQuestionsReportingFixtureLoader.LoadSubmissionText();
        var expectedFlat = AllQuestionsReportingFixtureLoader.LoadExpectedFlat();

        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(definitionJson);

        await using var dbContext = CreateContext(TenantId);
        var flattenedSubmissionRepository = CreateRepository(dbContext);
        var formSchemaRepository = CreateSchemaRepository(dbContext);
        FormSchema formSchema = new(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson);
        await formSchemaRepository.SaveAsync(formSchema, cancellationToken);

        var submission = CreateSubmission(submissionJson, isComplete: true);
        var submissionRepository = CreateSubmissionRepository(submission);

        var processor = CreateBackfillProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            formSchemaRepository);

        var result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        result.Processed.Should().Be(1);
        result.Failed.Should().Be(0);

        var persisted = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);

        using var actualDocument = JsonDocument.Parse(persisted.DataJson!);
        ReportingJsonAssertions.AssertJsonElementMatches(
            actualDocument.RootElement,
            expectedFlat,
            because: "backfilled all-questions submission should match the committed golden flat output");
    }

    private async Task SeedSimpleFormSchemaAsync(
        FormSchemaRepository formSchemaRepository,
        CancellationToken cancellationToken)
    {
        FormSchemaCompiler compiler = new();
        var compiled = compiler.CompilePersisted(SimpleDefinitionJson);
        FormSchema formSchema = new(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson);
        await formSchemaRepository.SaveAsync(formSchema, cancellationToken);
    }

    private static Submission CreateSubmission(string jsonData, bool isComplete)
    {
        var submission = Submission.Create(new SubmissionCreateArgs(
            TenantId: TenantId,
            FormId: FormId,
            FormDefinitionId: FormDefinitionId,
            JsonData: jsonData,
            IsComplete: isComplete));
        submission.Id = SubmissionId;
        return submission;
    }

    private static IRepository<Submission> CreateSubmissionRepository(Submission submission)
    {
        var submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<SubmissionBackfillPageSpec>(), Arg.Any<CancellationToken>())
            .Returns([new SubmissionBackfillCandidate(submission.Id, null, DateTime.UtcNow.AddDays(-1))]);
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        submissionRepository
            .AnyAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return submissionRepository;
    }

    private static SubmissionBackfillProcessor CreateBackfillProcessor(
        IRepository<Submission> submissionRepository,
        FlattenedSubmissionRepository flattenedSubmissionRepository,
        FormSchemaRepository formSchemaRepository)
    {
        FormSchemaProvider schemaProvider = new(
            formSchemaRepository,
            Substitute.For<IFormSchemaProcessor>(),
            Substitute.For<IFormsRepository>(),
            new FormSchemaCompiler());
        SubmissionFlatteningProcessor flatteningProcessor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            schemaProvider,
            NullLogger<SubmissionFlatteningProcessor>.Instance);

        return new SubmissionBackfillProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor,
            NullLogger<SubmissionBackfillProcessor>.Instance);
    }

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        // Clear tenants/data first — reporting InitialReporting seeds from "Tenants", and remigrating
        // with leftover snowflake tenant ids must not overflow (see hashtextextended seed ids).
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private ReportingDbContext CreateContext(long tenantId)
    {
        IntegrationTenantContext tenantContext = new(tenantId);

        var optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, tenantContext);
    }

    private static FlattenedSubmissionRepository CreateRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FlattenedSubmissionRepository(dbContext, unitOfWork);
    }

    private static FormSchemaRepository CreateSchemaRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormSchemaRepository(dbContext, unitOfWork);
    }
}
