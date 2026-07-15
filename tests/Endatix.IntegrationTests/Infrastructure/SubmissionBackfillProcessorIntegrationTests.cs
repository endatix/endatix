using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
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
    private const long SubmissionId = 500;
    private const string ProcessedDataJson = """{"firstName":"Ada"}""";

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
}
