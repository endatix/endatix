using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FlattenedSubmissionRepositoryTests
{
    private const long TenantId = 1;
    private const long OtherTenantId = 2;
    private const long FormId = 100;
    private const long SubmissionId = 500;
    private const string ProcessedDataJson = """{"firstName":"Ada"}""";

    private readonly DbIntegrationFixture _fixture;

    public FlattenedSubmissionRepositoryTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetBySubmissionIdAsync_WhenRowMissing_ReturnsNull()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);

        FlattenedSubmission? result = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubmissionIdAsync_WithOtherTenant_ReturnsNull()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        await repository.GetOrCreateAsync(TenantId, SubmissionId, FormId, cancellationToken);

        FlattenedSubmission? otherTenantResult = await repository.GetBySubmissionIdAsync(
            OtherTenantId,
            SubmissionId,
            cancellationToken);

        otherTenantResult.Should().BeNull();
        FlattenedSubmission? tenantResult = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        tenantResult.Should().NotBeNull();
        tenantResult!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task GetOrCreateAsync_OnSecondCall_ReturnsExistingRow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);

        FlattenedSubmission created = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);
        FlattenedSubmission existing = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);

        existing.SubmissionId.Should().Be(created.SubmissionId);
        (await dbContext.FlattenedSubmissions.CountAsync(cancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_WhenMarkProcessed_PersistsState()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        FlattenedSubmission row = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);

        row.MarkProcessed(ProcessedDataJson);
        await repository.SaveAsync(row, cancellationToken);

        FlattenedSubmission? persisted = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        persisted.DataJson.Should().Be(ProcessedDataJson);
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenMarkDeleted_PersistsAndExcludesFromQueries()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FlattenedSubmissionRepository repository = CreateRepository(dbContext);
        FlattenedSubmission row = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);

        row.MarkDeleted();
        await repository.SaveAsync(row, cancellationToken);

        row.IsDeleted.Should().BeTrue();
        FlattenedSubmission persisted = await dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .SingleAsync(row => row.SubmissionId == SubmissionId, cancellationToken);
        persisted.IsDeleted.Should().BeTrue();

        FlattenedSubmission? filtered = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        filtered.Should().BeNull("deleted rows are excluded by the global IsDeleted query filter");
    }

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
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
