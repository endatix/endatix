using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// PostgreSQL integration coverage for <see cref="ExportFormatRepository.SeedDefaultsAsync"/>, which adds
/// several rows in one batch and therefore needs its Ids assigned before the change tracker sees them.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ExportFormatRepositorySeedIntegrationTests
{
    private const long TenantId = 47;

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

        await using ReportingDbContext context = CreateReportingDbContext();
        ExportFormatRepository repository = CreateRepository(context);

        // Act
        await repository.SeedDefaultsAsync(TenantId, cancellationToken);

        // Assert
        List<ExportFormat> formats = await ReadFormatsAsync(cancellationToken);

        formats.Should().HaveCount(3);
        formats.Select(format => format.Name).Should().BeEquivalentTo("CSV", "JSON", "Codebook");
        formats.Select(format => format.Id).Should().OnlyHaveUniqueItems();
        formats.Select(format => format.Id).Should().AllSatisfy(id => id.Should().BePositive());
    }

    [Fact]
    public async Task SeedDefaultsAsync_TenantWithoutFormats_CreatesDefaultMappingToCsvFormat()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);

        await using ReportingDbContext context = CreateReportingDbContext();
        ExportFormatRepository repository = CreateRepository(context);

        // Act
        await repository.SeedDefaultsAsync(TenantId, cancellationToken);

        // Assert
        await using ReportingDbContext readContext = CreateReportingDbContext();
        long csvFormatId = await readContext.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == TenantId && format.Name == "CSV")
            .Select(format => format.Id)
            .SingleAsync(cancellationToken);

        List<SurveyTypeExportMapping> mappings = await readContext.SurveyTypeExportMappings
            .AsNoTracking()
            .Where(mapping => mapping.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        mappings.Should().ContainSingle();
        mappings[0].IsDefault.Should().BeTrue();
        mappings[0].SurveyTypeId.Should().BeNull();
        mappings[0].ExportFormatId.Should().Be(csvFormatId);
    }

    [Fact]
    public async Task SeedDefaultsAsync_TenantAlreadySeeded_LeavesExistingFormatsUnchanged()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await PrepareSchemaAsync(cancellationToken);

        await using (ReportingDbContext firstContext = CreateReportingDbContext())
        {
            await CreateRepository(firstContext).SeedDefaultsAsync(TenantId, cancellationToken);
        }

        List<long> idsAfterFirstSeed = (await ReadFormatsAsync(cancellationToken))
            .Select(format => format.Id)
            .OrderBy(id => id)
            .ToList();

        // Act
        await using (ReportingDbContext secondContext = CreateReportingDbContext())
        {
            await CreateRepository(secondContext).SeedDefaultsAsync(TenantId, cancellationToken);
        }

        // Assert
        List<long> idsAfterSecondSeed = (await ReadFormatsAsync(cancellationToken))
            .Select(format => format.Id)
            .OrderBy(id => id)
            .ToList();

        idsAfterSecondSeed.Should().Equal(idsAfterFirstSeed);
    }

    private async Task PrepareSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private async Task<List<ExportFormat>> ReadFormatsAsync(CancellationToken cancellationToken)
    {
        await using ReportingDbContext context = CreateReportingDbContext();

        return await context.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == TenantId)
            .ToListAsync(cancellationToken);
    }

    private ReportingDbContext CreateReportingDbContext()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, _idGenerator, tenantContext);
    }

    private ExportFormatRepository CreateRepository(ReportingDbContext context) =>
        new(
            context,
            new ReportingUnitOfWork(context),
            new ExportFormatSettingsParser(NullLogger<ExportFormatSettingsParser>.Instance),
            new ExportCapabilityRegistry(),
            _idGenerator);
}
