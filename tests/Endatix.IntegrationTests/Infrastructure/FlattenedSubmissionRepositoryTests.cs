using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
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
    // In the form PostgreSQL returns a jsonb value, which is how the conditional write stores it.
    private const string ProcessedDataJson = """{"firstName": "Ada"}""";

    private readonly DbIntegrationFixture _fixture;

    public FlattenedSubmissionRepositoryTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetBySubmissionIdAsync_WhenRowMissing_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);

        var result = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubmissionIdAsync_WithOtherTenant_ReturnsNull()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        await repository.EnsureExistsAsync(KeyOf(SubmissionId), cancellationToken);

        // Act
        var otherTenantResult = await repository.GetBySubmissionIdAsync(
            OtherTenantId,
            SubmissionId,
            cancellationToken);
        var tenantResult = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);

        // Assert
        otherTenantResult.Should().BeNull();
        tenantResult.Should().NotBeNull();
        tenantResult!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task EnsureExistsAsync_OnSecondCall_KeepsTheOneRow()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        await repository.EnsureExistsAsync(KeyOf(SubmissionId), cancellationToken);

        // Act
        await repository.EnsureExistsAsync(KeyOf(SubmissionId), cancellationToken);

        // Assert
        var row = await dbContext.FlattenedSubmissions.SingleAsync(cancellationToken);
        row.SubmissionId.Should().Be(SubmissionId);
        row.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Pending);
    }

    [Fact]
    public async Task TryMarkProcessedAsync_OnExistingRow_PersistsProcessedState()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        await repository.EnsureExistsAsync(KeyOf(SubmissionId), cancellationToken);
        var sourceModifiedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var landed = await repository.TryMarkProcessedAsync(
            new FlattenedRevision(TenantId, SubmissionId, Revision: 3, sourceModifiedAt),
            ProcessedDataJson,
            cancellationToken);

        // Assert
        landed.Should().BeTrue();
        var persisted = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Integration.Code.Should().Be(SubmissionIntegrationStatusCodes.Processed);
        persisted.Integration.ProcessedAt.Should().NotBeNull();
        persisted.DataJson.Should().Be(ProcessedDataJson);
        persisted.SourceRevision.Should().Be(3);
        persisted.SourceModifiedAt.Should().Be(sourceModifiedAt);
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WhenMarkDeleted_PersistsAndExcludesFromQueries()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        var row = await new FlattenedRowSeed(dbContext).PendingAsync(KeyOf(SubmissionId), cancellationToken);
        row.MarkDeleted();

        // Act
        await repository.SaveAsync(row, cancellationToken);

        // Assert
        row.IsDeleted.Should().BeTrue();
        var persisted = await dbContext.FlattenedSubmissions
            .IgnoreQueryFilters()
            .SingleAsync(row => row.SubmissionId == SubmissionId, cancellationToken);
        persisted.IsDeleted.Should().BeTrue();

        var filtered = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        filtered.Should().BeNull("deleted rows are excluded by the global IsDeleted query filter");
    }

    [Fact]
    public async Task DeleteByFormIdAsync_RemovesAllRowsForFormIncludingSoftDeleted()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);

        FlattenedRowSeed rows = new(dbContext);
        await rows.ProcessedAsync(KeyOf(SubmissionId), ProcessedDataJson, cancellationToken);
        await rows.SoftDeletedAsync(KeyOf(SubmissionId + 1), cancellationToken);
        await repository.EnsureExistsAsync(KeyOf(SubmissionId + 2, formId: FormId + 1), cancellationToken);

        // Act
        var deleted = await repository.DeleteByFormIdAsync(TenantId, FormId, cancellationToken);

        // Assert
        deleted.Should().Be(2);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.TenantId == TenantId && row.FormId == FormId, cancellationToken))
            .Should().Be(0);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.SubmissionId == SubmissionId + 2, cancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task DeleteBySubmissionIdAsync_RemovesTargetIncludingSoftDeleted()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);

        await new FlattenedRowSeed(dbContext).SoftDeletedAsync(KeyOf(SubmissionId), cancellationToken);
        await repository.EnsureExistsAsync(KeyOf(SubmissionId + 1), cancellationToken);

        // Act
        var deleted = await repository.DeleteBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);

        // Assert
        deleted.Should().Be(1);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.SubmissionId == SubmissionId, cancellationToken))
            .Should().Be(0);
        (await dbContext.FlattenedSubmissions
                .IgnoreQueryFilters()
                .CountAsync(row => row.SubmissionId == SubmissionId + 1, cancellationToken))
            .Should().Be(1);
    }

    private static FlattenedSubmissionKey KeyOf(long submissionId, long formId = FormId) =>
        new(TenantId, formId, submissionId);

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
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
}
