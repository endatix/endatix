using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;
using Endatix.Modules.Reporting.Features.ExportFormats;
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

    /// <summary>
    /// The idempotence guards must key off the target tenant, not the ambient one. Under the tenant
    /// query filter the second run reads nothing and re-inserts, violating the unique indexes on
    /// <c>(TenantId, Name)</c> and the tenant's single default mapping. Tenant 0 would not catch
    /// this — it bypasses the filter (see <see cref="ReportingQueryFilterTests"/>).
    /// </summary>
    [Fact]
    public async Task SeedDefaultsAsync_WhenAmbientTenantDiffers_SeedsAndDoesNotDuplicate()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long targetTenantId = 9104;
        const long ambientTenantId = 9105;
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

        SurveyTypeExportMapping mapping = await LoadDefaultMappingAsync(verify, targetTenantId, cancellationToken);
        mapping.ExportFormatId.Should().Be(
            formats.Single(format => format.DeliveryFormat == ExportDeliveryFormat.Csv).Id);
    }

    /// <summary>Soft-deleted rows stay hidden: the seed backfills the format the tenant no longer has.</summary>
    [Fact]
    public async Task SeedDefaultsAsync_WhenFormatSoftDeleted_ReseedsIt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9106;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        ExportFormat xlsx = await db.ExportFormats.SingleAsync(
            format => format.TenantId == tenantId && format.DeliveryFormat == ExportDeliveryFormat.Xlsx,
            cancellationToken);
        xlsx.Delete();
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        // Act
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Should().ContainSingle(format => format.DeliveryFormat == ExportDeliveryFormat.Xlsx)
            .Which.Id.Should().NotBe(xlsx.Id);
    }

    /// <summary>
    /// Unique mapping indexes exclude soft-deleted rows (same as ExportFormats). Otherwise a
    /// deleted tenant default still occupies IX_SurveyTypeExportMappings_TenantId, seed insert
    /// 23505s, and IsSameDefaultAsync cannot see the tombstone — GetTenantDefaultAsync stays null.
    /// </summary>
    [Fact]
    public async Task SeedDefaultsAsync_WhenDefaultMappingSoftDeleted_ReseedsIt()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9111;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        ExportFormatRepository repository = CreateRepository(db);
        await repository.SeedDefaultsAsync(tenantId, cancellationToken);

        SurveyTypeExportMapping mapping = await db.SurveyTypeExportMappings.SingleAsync(
            candidate => candidate.TenantId == tenantId && candidate.SurveyTypeId == null,
            cancellationToken);
        long deletedMappingId = mapping.Id;
        mapping.Delete();
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        db.ChangeTracker.Clear();
        SurveyTypeExportMapping reseeded = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);
        reseeded.Id.Should().NotBe(deletedMappingId);

        ExportFormatRecord? tenantDefault = await CreateRepository(db)
            .GetTenantDefaultAsync(tenantId, cancellationToken);
        tenantDefault.Should().NotBeNull();
        tenantDefault!.DeliveryFormat.Should().Be(ExportDeliveryFormat.Csv);
        tenantDefault.Profile.Should().Be(ExportProfile.Native);
    }

    /// <summary>
    /// A default mapping whose format was soft deleted reads as "no default" through
    /// <c>GetTenantDefaultAsync</c>'s filtered <c>Include</c>. Re-seeding repairs it rather than
    /// returning early on the mapping's mere existence.
    /// </summary>
    [Fact]
    public async Task SeedDefaultsAsync_WhenDefaultFormatSoftDeleted_RepointsMapping()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9107;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        ExportFormat csv = await db.ExportFormats.SingleAsync(
            format => format.TenantId == tenantId && format.DeliveryFormat == ExportDeliveryFormat.Csv,
            cancellationToken);
        csv.Delete();
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        // Act
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        db.ChangeTracker.Clear();
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        long reseededCsvId = formats.Single(format => format.DeliveryFormat == ExportDeliveryFormat.Csv).Id;
        reseededCsvId.Should().NotBe(csv.Id);

        SurveyTypeExportMapping mapping = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);
        mapping.ExportFormatId.Should().Be(reseededCsvId);
    }

    [Fact]
    public async Task SeedDefaultsAsync_WhenNameTakenByMatchingNativeCsv_SeedsRemainingAndKeepsMapping()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9108;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        db.ExportFormats.Add(new ExportFormat(
            tenantId, "CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        // Act
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Should().HaveCount(4);
        ExportFormat csv = formats.Should().ContainSingle(format => format.Name == "CSV").Subject;
        csv.Profile.Should().Be(ExportProfile.Native);
        SurveyTypeExportMapping mapping = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);
        mapping.ExportFormatId.Should().Be(csv.Id);
    }

    [Fact]
    public async Task SeedDefaultsAsync_WhenNameTakenByNonNativeCsv_SeedsRemainingFormatsWithoutNativeCsvMapping()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9109;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        db.ExportFormats.Add(new ExportFormat(
            tenantId, "CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Shoji));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        // Act
        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Should().ContainSingle(format => format.Name == "CSV")
            .Which.Profile.Should().Be(ExportProfile.Shoji);
        formats.Should().NotContain(format =>
            format.DeliveryFormat == ExportDeliveryFormat.Csv && format.Profile == ExportProfile.Native);
        formats.Should().Contain(format => format.DeliveryFormat == ExportDeliveryFormat.Json && format.Profile == ExportProfile.Native);
        formats.Should().Contain(format => format.DeliveryFormat == ExportDeliveryFormat.Xlsx);
        formats.Should().Contain(format => format.ExportTarget == ExportTarget.Codebook);
        SurveyTypeExportMapping? mapping = await db.SurveyTypeExportMappings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.IsDefault,
                cancellationToken);
        mapping.Should().BeNull();
    }

    [Fact]
    public async Task SeedDefaultsAsync_WhenTenantScopeMappingCleared_DoesNotInsertASecondRow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const long tenantId = 9110;
        await using ReportingDbContext db = await CreateMigratedContextAsync(tenantId, cancellationToken);
        ExportFormatRepository repository = CreateRepository(db);
        await repository.SeedDefaultsAsync(tenantId, cancellationToken);

        SurveyTypeExportMapping mapping = await db.SurveyTypeExportMappings
            .SingleAsync(
                candidate => candidate.TenantId == tenantId && candidate.SurveyTypeId == null,
                cancellationToken);
        mapping.ClearDefault();
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        await CreateRepository(db).SeedDefaultsAsync(tenantId, cancellationToken);

        List<SurveyTypeExportMapping> mappings = await db.SurveyTypeExportMappings
            .AsNoTracking()
            .Where(candidate => candidate.TenantId == tenantId && candidate.SurveyTypeId == null)
            .ToListAsync(cancellationToken);
        mappings.Should().ContainSingle();
        mappings[0].IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SeedDefaultExportFormats_ForExistingTenant_CreatesEveryNativeDefaultAndCsvMapping()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        long tenantId = await InsertTenantAsync(cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(
            _fixture.ConnectionString,
            _fixture.Provider,
            cancellationToken);
        await using ReportingDbContext db = CreateContext(tenantId);

        // Act — tenants created after InitialReporting still get the catalog rows
        string sql = SeedDefaultExportFormatsSql.Up.Replace("{", "{{").Replace("}", "}}");
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        // Assert
        List<ExportFormat> formats = await LoadFormatsAsync(db, tenantId, cancellationToken);
        formats.Should().HaveCount(DefaultExportFormats.All.Count);
        foreach (DefaultExportFormat definition in DefaultExportFormats.All)
        {
            formats.Should().ContainSingle(format =>
                format.Name == definition.Name &&
                format.ExportTarget == definition.Target &&
                format.DeliveryFormat == definition.Delivery &&
                format.Profile == ExportProfile.Native);
        }

        SurveyTypeExportMapping mapping = await LoadDefaultMappingAsync(db, tenantId, cancellationToken);
        ExportFormat csv = formats.Should().ContainSingle(format => format.Name == DefaultExportFormats.Csv.Name).Subject;
        mapping.ExportFormatId.Should().Be(csv.Id);
    }

    private async Task<long> InsertTenantAsync(CancellationToken cancellationToken)
    {
        IntegrationTenantContext tenantContext = IntegrationTenantContext.Bypass;
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
        IntegrationTenantContext tenantContext = new(tenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, ReportingTestSchema.ValueGeneratorFactory, tenantContext);
    }

    private static ExportFormatRepository CreateRepository(ReportingDbContext db) =>
        new(
            db,
            new ReportingUnitOfWork(db),
            new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance),
            new ExportCapabilityRegistry(),
            new UniqueConstraintViolationChecker());
}
