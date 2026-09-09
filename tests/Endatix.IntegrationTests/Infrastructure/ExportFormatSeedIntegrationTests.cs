using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// PostgreSQL coverage for <see cref="ExportFormatRepository.SeedDefaultsAsync"/>, which is an
/// idempotent "ensure" — it backfills only the default formats a tenant is missing.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ExportFormatSeedIntegrationTests
{
    private readonly DbIntegrationFixture _fixture;

    public ExportFormatSeedIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SeedDefaultsAsync_ForNewTenant_CreatesEveryDefaultFormatAndCsvMapping()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9101;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);

        // Act
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Select(format => (format.ExportTarget, format.DeliveryFormat)).Should().BeEquivalentTo(
        [
            (ExportTarget.Submissions, ExportDeliveryFormat.Csv),
            (ExportTarget.Submissions, ExportDeliveryFormat.Json),
            (ExportTarget.Submissions, ExportDeliveryFormat.Xlsx),
            (ExportTarget.Codebook, ExportDeliveryFormat.Json),
        ]);

        long csvId = formats.Single(format => format.DeliveryFormat == ExportDeliveryFormat.Csv).Id;
        SurveyTypeExportMapping mapping = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);
        mapping.ExportFormatId.Should().Be(csvId);
    }

    /// <summary>The shape existing tenants are in: the InitialReporting migration seeded CSV/JSON/Codebook, but no XLSX.</summary>
    [Fact]
    public async Task SeedDefaultsAsync_WhenXlsxMissing_AddsOnlyXlsxAndKeepsExistingMapping()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9102;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        ExportFormatRepository repository = CreateRepository(db);

        await repository.SeedDefaultsAsync(tenantId, cancellationToken);
        db.ChangeTracker.Clear();
        await db.ExportFormats
            .Where(format => format.TenantId == tenantId && format.DeliveryFormat == ExportDeliveryFormat.Xlsx)
            .ExecuteDeleteAsync(cancellationToken);

        SurveyTypeExportMapping mappingBefore = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);

        // Act
        await repository.SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Should().HaveCount(4);
        formats.Should().ContainSingle(format => format.DeliveryFormat == ExportDeliveryFormat.Xlsx);

        SurveyTypeExportMapping mappingAfter = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);
        mappingAfter.Id.Should().Be(mappingBefore.Id);
    }

    [Fact]
    public async Task SeedDefaultsAsync_WhenRunTwice_DoesNotDuplicateRows()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9103;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        ExportFormatRepository repository = CreateRepository(db);
        await repository.SeedDefaultsAsync(tenantId, cancellationToken);

        // Act
        await repository.SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Should().HaveCount(4);

        int mappingCount = await db.SurveyTypeExportMappings
            .AsNoTracking()
            .CountAsync(mapping => mapping.TenantId == tenantId, cancellationToken);
        mappingCount.Should().Be(1);
    }

    [Fact]
    public async Task SeedDefaultsAsync_WhenAmbientTenantDiffers_SeedsAndDoesNotDuplicate()
    {
        // Arrange — outbox tenant.created runs with app-level ITenantContext (0), not the new tenant
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long targetTenantId = 9104;
        const long ambientTenantId = 0;
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        await using ReportingDbContext first = CreateContext(ambientTenantId);
        await CreateRepository(first).SeedDefaultsAsync(targetTenantId, cancellationToken);

        await using ReportingDbContext second = CreateContext(ambientTenantId);

        // Act
        await CreateRepository(second).SeedDefaultsAsync(targetTenantId, cancellationToken);

        // Assert
        await using ReportingDbContext verify = CreateContext(targetTenantId);
        List<ExportFormat> formats = await LoadFormatsAsync(verify, targetTenantId, cancellationToken);
        formats.Should().HaveCount(4);
    }

    /// <summary>
    /// Reporting lists rows, not capabilities, so a new delivery format only reaches existing
    /// tenants through the <c>SeedXlsxExportFormat</c> data migration.
    /// </summary>
    [Fact]
    public async Task SeedXlsxExportFormatMigration_ForExistingTenant_CreatesExcelFormatRow()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        long tenantId = await InsertTenantAsync(cancellationToken);

        // Act
        await ReportingTestSchema.EnsureMigratedAsync(
            _fixture.ConnectionString,
            _fixture.Provider,
            cancellationToken);

        // Assert
        await using ReportingDbContext db = CreateContext(tenantId);
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        ExportFormat? xlsx = formats.SingleOrDefault(
            format => format.DeliveryFormat == ExportDeliveryFormat.Xlsx);

        xlsx.Should().NotBeNull();
        xlsx!.ExportTarget.Should().Be(ExportTarget.Submissions);
        xlsx.Profile.Should().Be(ExportProfile.Native);
        xlsx.Name.Should().Be("Excel (XLSX)");
        xlsx.SettingsJson.Should().NotContain("locale");
    }

    private async Task<long> InsertTenantAsync(CancellationToken cancellationToken)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        IncrementingIdGenerator idGenerator = new(Random.Shared.NextInt64(100_000, 900_000));
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);

        await using AppDbContext appDb = new(
            optionsBuilder.Options,
            idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(idGenerator),
            new OutboxIntegrationEventDispatcher());

        Tenant tenant = new($"xlsx-seed-{Guid.NewGuid():N}"[..32], Guid.NewGuid().ToString("N")[..8]);
        appDb.Set<Tenant>().Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }

    private static async Task<List<ExportFormat>> LoadFormatsAsync(
        ReportingDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        await db.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    private static async Task<SurveyTypeExportMapping> LoadDefaultMappingAsync(
        ReportingDbContext db,
        long tenantId,
        CancellationToken cancellationToken)
    {
        SurveyTypeExportMapping? mapping = await db.SurveyTypeExportMappings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.IsDefault &&
                    candidate.SurveyTypeId == null,
                cancellationToken);

        mapping.Should().NotBeNull();
        return mapping!;
    }

    private async Task<ReportingDbContext> CreateMigratedContextAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        return CreateContext(tenantId);
    }

    private ReportingDbContext CreateContext(long tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
    }

    private static ExportFormatRepository CreateRepository(ReportingDbContext db) =>
        new(
            db,
            new ReportingUnitOfWork(db),
            new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance),
            new ExportCapabilityRegistry());
}
