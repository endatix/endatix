using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
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
/// PostgreSQL integration coverage for <see cref="ExportFormatRepository.SeedDefaultsAsync"/>: the
/// multi-row insert, its idempotence guards across tenant scopes, and repair of a default mapping
/// left pointing at a soft-deleted format.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ExportFormatRepositorySeedIntegrationTests
{
    private const long TenantId = 47;
    private const long OtherTenantId = 48;

    private readonly DbIntegrationFixture _fixture;
    private readonly IncrementingIdGenerator _idGenerator = new();

    public ExportFormatRepositorySeedIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SeedDefaultsAsync_TenantWithoutFormats_CreatesDefaultsWithDistinctIds()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);

        // Act
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await ReadFormatsAsync(cancellationToken);

        formats.Should().HaveCount(3);
        formats.Select(format => format.Name).Should().BeEquivalentTo("CSV", "JSON", "Codebook");
        formats.Select(format => format.Id).Should().OnlyHaveUniqueItems();
        formats.Select(format => format.Id).Should().AllSatisfy(id => id.Should().BePositive());
    }

    [Fact]
    public async Task SeedDefaultsAsync_TenantWithoutFormats_CreatesFormatsWithTheirExportSettings()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);

        // Act
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await ReadFormatsAsync(cancellationToken);

        ExportFormat csv = formats.Single(format => format.Name == "CSV");
        csv.ExportTarget.Should().Be(ExportTarget.Submissions);
        csv.DeliveryFormat.Should().Be(ExportDeliveryFormat.Csv);
        csv.Profile.Should().Be(ExportProfile.Native);
        // Parsed, not string-matched: the column is jsonb and the server renormalises the text.
        ReadSetting(csv.SettingsJson, "aliasProfile").Should().Be("native");
        ReadSetting(csv.SettingsJson, "keySeparator").Should().Be("__");

        ExportFormat json = formats.Single(format => format.Name == "JSON");
        json.ExportTarget.Should().Be(ExportTarget.Submissions);
        json.DeliveryFormat.Should().Be(ExportDeliveryFormat.Json);

        ExportFormat codebook = formats.Single(format => format.Name == "Codebook");
        codebook.ExportTarget.Should().Be(ExportTarget.Codebook);
        codebook.DeliveryFormat.Should().Be(ExportDeliveryFormat.Json);
        ReadSetting(codebook.SettingsJson, "aliasProfile").Should().Be("native");
    }

    [Fact]
    public async Task SeedDefaultsAsync_TenantWithoutFormats_CreatesDefaultMappingToCsvFormat()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);

        // Act
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        // Assert
        long csvFormatId = (await ReadFormatsAsync(cancellationToken))
            .Single(format => format.Name == "CSV").Id;
        List<SurveyTypeExportMapping> mappings = await ReadMappingsAsync(cancellationToken);

        mappings.Should().ContainSingle();
        mappings[0].IsDefault.Should().BeTrue();
        mappings[0].SurveyTypeId.Should().BeNull();
        mappings[0].ExportFormatId.Should().Be(csvFormatId);
    }

    [Fact]
    public async Task SeedDefaultsAsync_CalledTwice_LeavesFormatsAndMappingUnchanged()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        List<long> formatIdsBefore = await ReadFormatIdsAsync(cancellationToken);
        List<long> mappingIdsBefore = (await ReadMappingsAsync(cancellationToken))
            .Select(mapping => mapping.Id)
            .OrderBy(id => id)
            .ToList();

        // Act
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        // Assert
        (await ReadFormatIdsAsync(cancellationToken)).Should().Equal(formatIdsBefore);
        (await ReadMappingsAsync(cancellationToken))
            .Select(mapping => mapping.Id)
            .OrderBy(id => id)
            .Should().Equal(mappingIdsBefore);
    }

    [Fact]
    public async Task SeedDefaultsAsync_AmbientTenantIsAnotherTenant_DoesNotDuplicateOnSecondCall()
    {
        // Arrange — provisioning seeds tenant A while the request scope belongs to tenant B. Under the
        // tenant query filter the idempotence guards read TenantId == B AND TenantId == A, match
        // nothing, and try to seed a second full set. The duplicate rows never land — the unique index
        // on (TenantId, Name) is filtered on IsDeleted, not scoped to a tenant — so what the guards
        // actually prevent is a wasted insert that fails with 23505 and, before this changed, surfaced
        // as an unhandled DbUpdateException.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);

        await SeedAsync(TenantId, ambientTenantId: OtherTenantId, cancellationToken);

        // Act
        await SeedAsync(TenantId, ambientTenantId: OtherTenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await ReadFormatsAsync(cancellationToken);
        formats.Should().HaveCount(3);
        formats.Select(format => format.Name).Should().OnlyHaveUniqueItems();
        (await ReadMappingsAsync(cancellationToken)).Should().ContainSingle();
    }

    [Fact]
    public async Task SeedDefaultsAsync_FormatsSoftDeleted_RepointsDefaultMappingAtNewCsvFormat()
    {
        // Arrange — the mapping survives a soft delete of the format it references. Left alone it
        // resolves through an Include that the soft-delete filter nulls out, so the tenant reads as
        // having no default export format at all.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        await SoftDeleteAllFormatsAsync(cancellationToken);

        // Act
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await ReadFormatsAsync(cancellationToken);
        formats.Should().HaveCount(3, "the soft-deleted set is replaced, not resurrected");

        long csvFormatId = formats.Single(format => format.Name == "CSV").Id;
        List<SurveyTypeExportMapping> mappings = await ReadMappingsAsync(cancellationToken);

        mappings.Should().ContainSingle();
        mappings[0].ExportFormatId.Should().Be(csvFormatId);

        await using ReportingDbContext context = CreateReportingDbContext(TenantId);
        ExportFormatRecord? tenantDefault = await CreateRepository(context)
            .GetTenantDefaultAsync(TenantId, cancellationToken);

        tenantDefault.Should().NotBeNull();
        tenantDefault!.Id.Should().Be(csvFormatId);
    }

    [Fact]
    public async Task SeedDefaultsAsync_DefaultMappingMissing_CreatesItWithoutRecreatingFormats()
    {
        // Arrange — the state a crash between the old two-phase writes would leave behind: formats
        // committed, mapping never written. The formats-exist guard used to make it unrepairable.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        await DeleteAllMappingsAsync(cancellationToken);
        List<long> formatIdsBefore = await ReadFormatIdsAsync(cancellationToken);

        // Act
        await SeedAsync(TenantId, ambientTenantId: TenantId, cancellationToken);

        // Assert
        (await ReadFormatIdsAsync(cancellationToken)).Should().Equal(formatIdsBefore);
        (await ReadMappingsAsync(cancellationToken)).Should().ContainSingle();
    }

    private static string? ReadSetting(string? settingsJson, string propertyName)
    {
        using JsonDocument document = JsonDocument.Parse(settingsJson!);

        return document.RootElement.TryGetProperty(propertyName, out JsonElement value)
            ? value.GetString()
            : null;
    }

    private async Task SeedAsync(long tenantId, long ambientTenantId, CancellationToken cancellationToken)
    {
        await using ReportingDbContext context = CreateReportingDbContext(ambientTenantId);
        await CreateRepository(context).SeedDefaultsAsync(tenantId, cancellationToken);
    }

    private async Task SoftDeleteAllFormatsAsync(CancellationToken cancellationToken)
    {
        await using ReportingDbContext context = CreateReportingDbContext(TenantId);

        List<ExportFormat> formats = await context.ExportFormats
            .Where(format => format.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        foreach (ExportFormat format in formats)
        {
            format.Delete();
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteAllMappingsAsync(CancellationToken cancellationToken)
    {
        await using ReportingDbContext context = CreateReportingDbContext(TenantId);

        await context.SurveyTypeExportMappings
            .IgnoreQueryFilters()
            .Where(mapping => mapping.TenantId == TenantId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task PrepareSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private async Task<List<long>> ReadFormatIdsAsync(CancellationToken cancellationToken) =>
        (await ReadFormatsAsync(cancellationToken))
            .Select(format => format.Id)
            .OrderBy(id => id)
            .ToList();

    private async Task<List<ExportFormat>> ReadFormatsAsync(CancellationToken cancellationToken)
    {
        await using ReportingDbContext context = CreateReportingDbContext(TenantId);

        return await context.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == TenantId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SurveyTypeExportMapping>> ReadMappingsAsync(CancellationToken cancellationToken)
    {
        await using ReportingDbContext context = CreateReportingDbContext(TenantId);

        return await context.SurveyTypeExportMappings
            .AsNoTracking()
            .Where(mapping => mapping.TenantId == TenantId)
            .ToListAsync(cancellationToken);
    }

    private ReportingDbContext CreateReportingDbContext(long ambientTenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(ambientTenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(
            optionsBuilder.Options,
            _idGenerator,
            tenantContext,
            new EfCoreValueGeneratorFactory(_idGenerator));
    }

    private static ExportFormatRepository CreateRepository(ReportingDbContext context) =>
        new(
            context,
            new ReportingUnitOfWork(context),
            new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance),
            new ExportCapabilityRegistry());
}
