using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
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
    private const string ProcessedDataJson = """{"firstName":"Ada"}""";
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository flattenedSubmissionRepository = CreateRepository(dbContext);
        FlattenedSubmission row = await flattenedSubmissionRepository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);
        row.MarkProcessed(ProcessedDataJson);
        await flattenedSubmissionRepository.SaveAsync(row, cancellationToken);

        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<CompletedSubmissionIdsForBackfillSpec>(), cancellationToken)
            .Returns([SubmissionId]);

        ISubmissionFlatteningProcessor flatteningProcessor = Substitute.For<ISubmissionFlatteningProcessor>();
        SubmissionBackfillProcessor processor = new(
            submissionRepository,
            flattenedSubmissionRepository,
            flatteningProcessor,
            NullLogger<SubmissionBackfillProcessor>.Instance);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        result.Skipped.Should().Be(1);
        result.Processed.Should().Be(0);
        await flatteningProcessor.DidNotReceive()
            .ProcessAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());

        FlattenedSubmission? persisted = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository flattenedSubmissionRepository = CreateRepository(dbContext);
        FormSchemaRepository formSchemaRepository = CreateSchemaRepository(dbContext);
        await SeedSimpleFormSchemaAsync(formSchemaRepository, cancellationToken);

        Submission submission = CreateSubmission(SimpleSubmissionJson, isComplete: true);
        IRepository<Submission> submissionRepository = CreateSubmissionRepository(submission);

        SubmissionBackfillProcessor processor = CreateBackfillProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            formSchemaRepository);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        result.Processed.Should().Be(1);
        result.Skipped.Should().Be(0);
        result.Failed.Should().Be(0);

        FlattenedSubmission? persisted = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        persisted.DataJson.Should().NotBeNullOrWhiteSpace();

        using JsonDocument actualDocument = JsonDocument.Parse(persisted.DataJson!);
        actualDocument.RootElement.GetProperty("q1").GetString().Should().Be("hello");
    }

    [Fact]
    public async Task BackfillFormAsync_WithAllQuestionsSubmission_ProducesGoldenFlatOutput()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        string definitionJson = AllQuestionsReportingFixtureLoader.LoadDefinitionText();
        string submissionJson = AllQuestionsReportingFixtureLoader.LoadSubmissionText();
        JsonElement expectedFlat = AllQuestionsReportingFixtureLoader.LoadExpectedFlat();

        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository flattenedSubmissionRepository = CreateRepository(dbContext);
        FormSchemaRepository formSchemaRepository = CreateSchemaRepository(dbContext);
        FormSchema formSchema = new(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson);
        await formSchemaRepository.SaveAsync(formSchema, cancellationToken);

        Submission submission = CreateSubmission(submissionJson, isComplete: true);
        IRepository<Submission> submissionRepository = CreateSubmissionRepository(submission);

        SubmissionBackfillProcessor processor = CreateBackfillProcessor(
            submissionRepository,
            flattenedSubmissionRepository,
            formSchemaRepository);

        SubmissionBackfillResult result = await processor.BackfillFormAsync(
            TenantId,
            FormId,
            new SubmissionBackfillOptions(),
            cancellationToken);

        result.Processed.Should().Be(1);
        result.Failed.Should().Be(0);

        FlattenedSubmission? persisted = await flattenedSubmissionRepository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);

        using JsonDocument actualDocument = JsonDocument.Parse(persisted.DataJson!);
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
        FormSchemaCompileResult compiled = compiler.CompilePersisted(SimpleDefinitionJson);
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
        Submission submission = Submission.Create(new SubmissionCreateArgs(
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
        IRepository<Submission> submissionRepository = Substitute.For<IRepository<Submission>>();
        submissionRepository
            .ListAsync(Arg.Any<CompletedSubmissionIdsForBackfillSpec>(), Arg.Any<CancellationToken>())
            .Returns([submission.Id]);
        submissionRepository
            .SingleOrDefaultAsync(Arg.Any<SubmissionWithDefinitionAndFormSpec>(), Arg.Any<CancellationToken>())
            .Returns(submission);
        return submissionRepository;
    }

    private static SubmissionBackfillProcessor CreateBackfillProcessor(
        IRepository<Submission> submissionRepository,
        FlattenedSubmissionRepository flattenedSubmissionRepository,
        FormSchemaRepository formSchemaRepository)
    {
        FormSchemaProvider schemaProvider = new(formSchemaRepository, Substitute.For<IFormSchemaProcessor>());
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
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, cancellationToken);
    }

    private ReportingDbContext CreateContext(long tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
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
