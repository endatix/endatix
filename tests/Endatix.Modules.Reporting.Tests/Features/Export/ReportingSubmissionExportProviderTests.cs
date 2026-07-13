using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class TabularExportDataSourceTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    [Fact]
    public async Task PrepareOptionsAsync_WithMissingSchema_ReturnsMissingSchemaMessage()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        TabularExportDataSource dataSource = new(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext();

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Save or publish the form definition", StringComparison.Ordinal));
        await reportingExportRepository.DidNotReceive()
            .HasExportableRowsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithInvalidSchemaArtifacts_ReturnsInvalidArtifactsMessage()
    {
        FormSchemaEntity schema = new(TenantId, FormId, 1, flatteningMap: " ", codebook: " ");
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        TabularExportDataSource dataSource = new(formSchemaRepository, reportingExportRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(CreateContext(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("schema artifacts are incomplete or invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithNoExportableRows_ReturnsMissingRowsMessage()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(false);

        TabularExportDataSource dataSource = new(formSchemaRepository, reportingExportRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(CreateContext(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Run admin backfill", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithSchemaAndRows_ReturnsColumnPlan()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(true);

        TabularExportDataSource dataSource = new(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext();

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey(SubmissionExportMetadataKeys.ColumnPlan);
        SubmissionExportColumnPlan columnPlan = (SubmissionExportColumnPlan)result.Value.Metadata![SubmissionExportMetadataKeys.ColumnPlan];
        columnPlan.Columns.Should().NotBeEmpty();
        columnPlan.Columns.Should().Contain(column => column.CanonicalKey == SubmissionExportRow.SystemColumns.FormId);
    }

    [Fact]
    public async Task StreamAsync_ForwardsQueryAndMapsRows()
    {
        const int exportPageSize = 42;
        DateTime createdAt = new(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTime modifiedAt = new(2024, 1, 3, 4, 5, 6, DateTimeKind.Utc);
        DateTime completedAt = new(2024, 1, 4, 5, 6, 7, DateTimeKind.Utc);
        FlattenedExportRow sourceRow = new(
            SubmissionId: 10,
            FormId: FormId,
            IsComplete: true,
            CreatedAt: createdAt,
            ModifiedAt: modifiedAt,
            CompletedAt: completedAt,
            SubmitterId: 99,
            SubmitterDisplayId: "sub-99",
            DataJson: """{"q1":"answer"}""");

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .StreamFlattenedSubmissionsAsync(
                TenantId,
                FormId,
                Arg.Is<ExportQueryOptions>(options => options.PageSize == exportPageSize),
                Arg.Any<CancellationToken>())
            .Returns(StreamRows(sourceRow));

        TabularExportDataSource dataSource = new(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext(exportPageSize: exportPageSize);

        List<SubmissionExportRow> rows = [];
        await foreach (IExportItem item in dataSource.StreamAsync(context, TestContext.Current.CancellationToken))
        {
            rows.Add((SubmissionExportRow)item);
        }

        rows.Should().ContainSingle();
        SubmissionExportRow mappedRow = rows[0];
        mappedRow.Id.Should().Be(sourceRow.SubmissionId);
        mappedRow.FormId.Should().Be(sourceRow.FormId);
        mappedRow.AnswersModel.Should().Be(sourceRow.DataJson);
    }

    [Fact]
    public void Matches_ReturnsTrueOnlyForTabularSubmissionExportWithoutSqlFunction()
    {
        TabularExportDataSource dataSource = new(
            Substitute.For<IFormSchemaRepository>(),
            Substitute.For<IReportingExportRepository>());

        dataSource.Matches(new ExportDataSourceRequest(TabularExportFormats.Csv, typeof(SubmissionExportRow), null)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest(TabularExportFormats.Json, typeof(SubmissionExportRow), null)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest("codebook", typeof(SubmissionExportRow), null)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("xml", typeof(SubmissionExportRow), null)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest(TabularExportFormats.Csv, typeof(SubmissionExportRow), "custom_fn")).Should().BeFalse();
    }

    private static ExportDataSourceContext CreateContext(int? exportPageSize = null) =>
        new(
            new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null),
            TenantId,
            FormId,
            new ExportOptions(),
            exportPageSize);

    private static async IAsyncEnumerable<FlattenedExportRow> StreamRows(params FlattenedExportRow[] rows)
    {
        foreach (FlattenedExportRow row in rows)
        {
            yield return row;
        }
    }
}

public sealed class ShojiCodebookExportDataSourceTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    [Fact]
    public async Task PrepareOptionsAsync_WithMissingSchema_ReturnsMissingSchemaMessage()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        ShojiCodebookExportDataSource dataSource = new(formSchemaRepository);

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

        ShojiCodebookExportDataSource dataSource = new(formSchemaRepository);

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

        ShojiCodebookExportDataSource dataSource = new(formSchemaRepository);
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
        string expectedCodebookJson = ShojiCodebookGenerator.Generate(schema.FlatteningMap, schema.Codebook);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        ShojiCodebookExportDataSource dataSource = new(formSchemaRepository);
        ExportDataSourceContext context = CreateContext();

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
    public void Matches_ReturnsTrueOnlyForCodebookDynamicExportRow()
    {
        ShojiCodebookExportDataSource dataSource = new(Substitute.For<IFormSchemaRepository>());

        dataSource.Matches(new ExportDataSourceRequest("codebook", typeof(DynamicExportRow), null)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest("json", typeof(DynamicExportRow), null)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("codebook", typeof(SubmissionExportRow), null)).Should().BeFalse();
    }

    private static ExportDataSourceContext CreateContext() =>
        new(
            new ExportDataSourceRequest("codebook", typeof(DynamicExportRow), null),
            TenantId,
            FormId,
            new ExportOptions(),
            ExportPageSize: null);
}
