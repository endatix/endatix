using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Tests.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch.Shoji;

public sealed class ShojiCodebookExportDataSourceTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    private static readonly ExportFormatSettingsParser ExportFormatSettingsParser =
        new(NullLogger<ExportFormatSettingsParser>.Instance);

    private static ShojiCodebookExportDataSource CreateDataSource(IFormSchemaRepository formSchemaRepository) =>
        new(formSchemaRepository, ExportFormatSettingsParser, TestExportCapabilityRegistry.Instance);

    [Fact]
    public async Task PrepareOptionsAsync_WithMissingSchema_ReturnsMissingSchemaMessage()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        ShojiCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(
            CreateContext(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Save or publish the form definition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithInvalidSchemaArtifacts_ReturnsInvalidArtifactsMessage()
    {
        FormSchemaEntity schema = new(TenantId, FormId, 1, flatteningMap: " ", codebook: " ");
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        ShojiCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(
            CreateContext(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("schema artifacts are incomplete or invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithValidSchema_ReturnsOptions()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        ShojiCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);
        ExportDataSourceContext context = CreateContext();

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(
            context,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(context.Options);
    }

    [Fact]
    public async Task StreamAsync_WithValidSchema_ReturnsShojiCodebookFromArtifacts()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
        string expectedCodebookJson = ShojiCodebookGenerator.Generate(
            schema.FlatteningMap,
            schema.Codebook,
            ExportFormatSettings.InterimCrunchKeySeparator);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        ShojiCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);
        ExportDataSourceContext context = CreateContext(
            settingsJson: """{"keySeparator":"--"}""");

        Result<ExportOptions> prepareResult = await dataSource.PrepareOptionsAsync(
            context,
            TestContext.Current.CancellationToken);
        prepareResult.IsSuccess.Should().BeTrue();

        List<IExportItem> items = [];
        await foreach (IExportItem item in dataSource.StreamAsync(context, TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.Should().ContainSingle().Which.Should().BeOfType<DynamicExportRow>();
        DynamicExportRow row = (DynamicExportRow)items[0];
        using JsonDocument actualDocument = JsonDocument.Parse(row.Data!);
        using JsonDocument expectedDocument = JsonDocument.Parse(expectedCodebookJson);
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedDocument.RootElement,
            because: "Shoji data source should generate codebook from persisted schema artifacts");
    }

    [Fact]
    public void Matches_ReturnsTrueOnlyForCodebookShojiDynamicExportRow()
    {
        ShojiCodebookExportDataSource dataSource = CreateDataSource(Substitute.For<IFormSchemaRepository>());

        dataSource.Matches(new ExportDataSourceRequest("codebook-shoji", typeof(DynamicExportRow), null)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest("json", typeof(DynamicExportRow), null)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("codebook-shoji", typeof(SubmissionExportRow), null)).Should().BeFalse();
    }

    private static ExportDataSourceContext CreateContext(string? settingsJson = null)
    {
        ExportOptions options = new();
        if (settingsJson is not null)
        {
            options.Metadata = new Dictionary<string, object>
            {
                [SubmissionExportMetadataKeys.ExecutionSettings] = new SubmissionExportExecutionSettings(
                    ExportFormatId: 1,
                    SettingsJson: settingsJson,
                    IncludeTestSubmissions: null,
                    ColumnScope: null),
            };
        }

        return new ExportDataSourceContext(
            new ExportDataSourceRequest("codebook-shoji", typeof(DynamicExportRow), null),
            TenantId,
            FormId,
            options,
            ExportPageSize: null);
    }
}
