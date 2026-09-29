using Endatix.Infrastructure.Data;
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
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        await repository.GetOrCreateAsync(TenantId, SubmissionId, FormId, cancellationToken);

        var otherTenantResult = await repository.GetBySubmissionIdAsync(
            OtherTenantId,
            SubmissionId,
            cancellationToken);

        otherTenantResult.Should().BeNull();
        var tenantResult = await repository.GetBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);
        tenantResult.Should().NotBeNull();
        tenantResult!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task GetOrCreateAsync_OnSecondCall_ReturnsExistingRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);

        var created = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);
        var existing = await repository.GetOrCreateAsync(
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
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        var row = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);

        row.MarkProcessed(ProcessedDataJson, DateTime.UtcNow);
        await repository.SaveAsync(row, cancellationToken);

        var persisted = await repository.GetBySubmissionIdAsync(
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
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);
        var row = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);

        row.MarkDeleted();
        await repository.SaveAsync(row, cancellationToken);

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

        var active = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);
        active.MarkProcessed(ProcessedDataJson, DateTime.UtcNow);
        await repository.SaveAsync(active, cancellationToken);

        var softDeleted = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId + 1,
            FormId,
            cancellationToken);
        softDeleted.MarkDeleted();
        await repository.SaveAsync(softDeleted, cancellationToken);

        var otherForm = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId + 2,
            formId: FormId + 1,
            cancellationToken);
        await repository.SaveAsync(otherForm, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using var dbContext = CreateContext(TenantId);
        var repository = CreateRepository(dbContext);

        var softDeleted = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId,
            FormId,
            cancellationToken);
        softDeleted.MarkDeleted();
        await repository.SaveAsync(softDeleted, cancellationToken);

        var other = await repository.GetOrCreateAsync(
            TenantId,
            SubmissionId + 1,
            FormId,
            cancellationToken);
        await repository.SaveAsync(other, cancellationToken);

        var deleted = await repository.DeleteBySubmissionIdAsync(
            TenantId,
            SubmissionId,
            cancellationToken);

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
