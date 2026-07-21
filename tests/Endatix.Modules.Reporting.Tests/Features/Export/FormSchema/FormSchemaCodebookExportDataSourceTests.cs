using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export.FormSchema;

public sealed class FormSchemaCodebookExportDataSourceTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    private static readonly ExportFormatSettingsParser ExportFormatSettingsParser =
        new(NullLogger<ExportFormatSettingsParser>.Instance);

    private static FormSchemaCodebookExportDataSource CreateDataSource(
        IFormSchemaRepository formSchemaRepository) =>
        new(formSchemaRepository, ExportFormatSettingsParser);

    [Fact]
    public async Task StreamAsync_WithValidSchema_ReturnsPersistedCodebookJson()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        FormSchemaCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);
        ExportDataSourceContext context = CreateContext();

        List<IExportItem> items = [];
        await foreach (IExportItem item in dataSource.StreamAsync(context, TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.Should().ContainSingle().Which.Should().BeOfType<DynamicExportRow>();
        DynamicExportRow row = (DynamicExportRow)items[0];
        using JsonDocument actualDocument = JsonDocument.Parse(row.Data!);
        using JsonDocument expectedDocument = JsonDocument.Parse(compiled.CodebookJson);
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedDocument.RootElement,
            because: "native codebook data source should stream persisted schema codebook JSON");
    }

    [Fact]
    public async Task StreamAsync_IgnoresLocaleAndStreamsPersistedMultiLocaleCodebook()
    {
        const string codebookJson = """
            {
              "version": 1,
              "locales": ["default", "es"],
              "questions": {
                "q1": {
                  "surveyJsType": "text",
                  "title": { "default": "Name", "es": "Nombre" },
                  "exportShape": "scalar"
                }
              },
              "columns": {},
              "choiceCatalogs": {}
            }
            """;
        FormSchemaEntity schema = new(
            TenantId,
            FormId,
            1,
            """{"version":1,"columns":[]}""",
            codebookJson,
            """["default","es"]""");

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        FormSchemaCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);
        ExportOptions options = new()
        {
            Metadata = new Dictionary<string, object>
            {
                [SubmissionExportMetadataKeys.ResolvedFormatSettings] =
                    ExportFormatSettings.Default with { Locale = "es" },
            },
        };

        List<IExportItem> items = [];
        await foreach (IExportItem item in dataSource.StreamAsync(
            CreateContext(options),
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.Should().ContainSingle().Which.Should().BeOfType<DynamicExportRow>();
        DynamicExportRow row = (DynamicExportRow)items[0];
        row.Data.Should().Be(codebookJson);
    }

    [Fact]
    public async Task StreamAsync_WithMissingSchema_YieldsNoRows()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        FormSchemaCodebookExportDataSource dataSource = CreateDataSource(formSchemaRepository);

        List<IExportItem> items = [];
        await foreach (IExportItem item in dataSource.StreamAsync(
            CreateContext(),
            TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        items.Should().BeEmpty();
    }

    [Fact]
    public void Matches_ReturnsTrueOnlyForCodebookNativeDynamicExportRow()
    {
        FormSchemaCodebookExportDataSource dataSource = CreateDataSource(
            Substitute.For<IFormSchemaRepository>());

        dataSource.Matches(new ExportDataSourceRequest("codebook", typeof(DynamicExportRow), null)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest("codebook-shoji", typeof(DynamicExportRow), null)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("codebook", typeof(SubmissionExportRow), null)).Should().BeFalse();
    }

    private static ExportDataSourceContext CreateContext(ExportOptions? options = null) =>
        new(
            new ExportDataSourceRequest("codebook", typeof(DynamicExportRow), null),
            TenantId,
            FormId,
            options ?? new ExportOptions(),
            ExportPageSize: null);
}
