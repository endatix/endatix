using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Tabular;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Tabular;

public sealed class TabularExportDataSourceTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    private static readonly ExportFormatSettingsParser ExportFormatSettingsParser =
        new(NullLogger<ExportFormatSettingsParser>.Instance);

    [Fact]
    public async Task PrepareOptionsAsync_WithMissingSchema_ReturnsMissingSchemaMessage()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext();

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Save or publish the form definition", StringComparison.Ordinal));
        await reportingExportRepository.DidNotReceive()
            .HasExportableRowsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>());
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
        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(CreateContext(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
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
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(false);

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(CreateContext(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
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
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(true);

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext();

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey(SubmissionExportMetadataKeys.ColumnPlan);
        SubmissionExportColumnPlan columnPlan = (SubmissionExportColumnPlan)result.Value.Metadata![SubmissionExportMetadataKeys.ColumnPlan];
        columnPlan.Columns.Should().NotBeEmpty();
        columnPlan.Columns.Should().Contain(column => column.CanonicalKey == SubmissionExportRow.SystemColumns.FormId);
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithCsv_AppliesTenantKeySeparatorToColumnPlan()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(true);

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext(
            format: "csv",
            settingsJson: """{"keySeparator":"--"}""");

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        SubmissionExportColumnPlan columnPlan = (SubmissionExportColumnPlan)result.Value.Metadata![SubmissionExportMetadataKeys.ColumnPlan];
        columnPlan.Columns.First(column => column.CanonicalKey == "qTagBox__adidas").ExportKey
            .Should().Be("qTagBox--adidas");
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithJson_KeepsDefaultKeySeparator()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(true);

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext(format: "json");

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        SubmissionExportColumnPlan columnPlan = (SubmissionExportColumnPlan)result.Value.Metadata![SubmissionExportMetadataKeys.ColumnPlan];
        columnPlan.Columns.First(column => column.CanonicalKey == "qTagBox__adidas").ExportKey
            .Should().Be("qTagBox__adidas");
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithCrunchSettings_AppliesAliasProfile()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadAllQuestionsText("all-questions-definition.json");
        IReadOnlyDictionary<string, string> expectedExportKeys = FormSchemaFixtureLoader.LoadAllQuestionsExpectedCrunchExportKeys();
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(TenantId, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .HasExportableRowsAsync(TenantId, FormId, Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>())
            .Returns(true);

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
        ExportOptions options = new()
        {
            Metadata = new Dictionary<string, object>
            {
                [SubmissionExportMetadataKeys.ExecutionSettings] = new SubmissionExportExecutionSettings(
                    SettingsJson: """{"aliasProfile":"crunch"}"""),
            },
        };
        ExportDataSourceContext context = new(
            new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null),
            TenantId,
            FormId,
            options,
            null);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(context, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        SubmissionExportColumnPlan columnPlan = (SubmissionExportColumnPlan)result.Value.Metadata![SubmissionExportMetadataKeys.ColumnPlan];
        Dictionary<string, string> actualExportKeys = columnPlan.Columns.ToDictionary(
            column => column.CanonicalKey,
            column => column.ExportKey,
            StringComparer.Ordinal);
        actualExportKeys.Should().BeEquivalentTo(expectedExportKeys);
    }

    [Fact]
    public async Task StreamAsync_ExcludesTestSubmissionsByDefault()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        reportingExportRepository
            .StreamFlattenedSubmissionsAsync(
                TenantId,
                FormId,
                Arg.Is<ExportQueryOptions>(query => query.IncludeTestSubmissions == false),
                Arg.Any<CancellationToken>())
            .Returns(StreamRows());

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
        ExportDataSourceContext context = CreateContext();

        await foreach (IExportItem _ in dataSource.StreamAsync(context, TestContext.Current.CancellationToken))
        {
        }

        reportingExportRepository.Received(1).StreamFlattenedSubmissionsAsync(
            TenantId,
            FormId,
            Arg.Is<ExportQueryOptions>(query => query.IncludeTestSubmissions == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamAsync_ForwardsQueryAndMapsRows()
    {
        const int exportPageSize = 42;
        DateTime createdAt = new(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTime modifiedAt = new(2024, 1, 3, 4, 5, 6, DateTimeKind.Utc);
        DateTime startedAt = new(2024, 1, 3, 10, 0, 0, DateTimeKind.Utc);
        DateTime completedAt = new(2024, 1, 4, 5, 6, 7, DateTimeKind.Utc);
        FlattenedExportRow sourceRow = new(
            SubmissionId: 10,
            FormId: FormId,
            IsComplete: true,
            CreatedAt: createdAt,
            ModifiedAt: modifiedAt,
            StartedAt: startedAt,
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

        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);
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
        mappedRow.CreatedAt.Should().Be(createdAt);
        mappedRow.ModifiedAt.Should().Be(modifiedAt);
        mappedRow.StartedAt.Should().Be(startedAt);
        mappedRow.CompletedAt.Should().Be(completedAt);
        mappedRow.AnswersModel.Should().Be(sourceRow.DataJson);
        SubmissionExportRow.CalculateDurationSeconds(mappedRow.StartedAt, mappedRow.CompletedAt)
            .Should().Be((long)Math.Floor((completedAt - startedAt).TotalSeconds));
    }

    [Fact]
    public async Task PrepareOptionsAsync_WithUnknownLocale_ReturnsInvalid()
    {
        string definitionJson = FormSchemaFixtureLoader.LoadText("simple-definition.json");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult compiled = compiler.CompilePersisted(definitionJson);
        FormSchemaEntity schema = new(
            TenantId,
            FormId,
            1,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);

        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        TabularExportDataSource dataSource = CreateDataSource(formSchemaRepository, reportingExportRepository);

        Result<ExportOptions> result = await dataSource.PrepareOptionsAsync(
            CreateContext(
                settingsJson: null,
                locale: "zz-not-a-locale"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().Contain(error =>
            error.ErrorMessage.Contains("Locale 'zz-not-a-locale' is not available", StringComparison.Ordinal));
        await reportingExportRepository.DidNotReceive()
            .HasExportableRowsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<ExportQueryOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Matches_ReturnsTrueOnlyForTabularSubmissionExportWithExportFormatId()
    {
        TabularExportDataSource dataSource = CreateDataSource(
            Substitute.For<IFormSchemaRepository>(),
            Substitute.For<IReportingExportRepository>());

        dataSource.Matches(new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null, 100L)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest("json", typeof(SubmissionExportRow), null, 100L)).Should().BeTrue();
        dataSource.Matches(new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), null)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("codebook", typeof(SubmissionExportRow), null, 100L)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("xml", typeof(SubmissionExportRow), null, 100L)).Should().BeFalse();
        dataSource.Matches(new ExportDataSourceRequest("csv", typeof(SubmissionExportRow), "custom_fn", 100L)).Should().BeFalse();
    }

    private static TabularExportDataSource CreateDataSource(
        IFormSchemaRepository formSchemaRepository,
        IReportingExportRepository reportingExportRepository) =>
        new(formSchemaRepository, reportingExportRepository, ExportFormatSettingsParser, ColumnAliasTransformerRegistry.Default);

    private static ExportDataSourceContext CreateContext(
        string format = "csv",
        int? exportPageSize = null,
        string? settingsJson = null,
        string? locale = null)
    {
        ExportOptions options = new();
        if (settingsJson is not null || locale is not null)
        {
            options.Metadata = new Dictionary<string, object>
            {
                [SubmissionExportMetadataKeys.ExecutionSettings] = new SubmissionExportExecutionSettings(
                    ExportFormatId: 1,
                    SettingsJson: settingsJson,
                    IncludeTestSubmissions: null,
                    ColumnScope: null,
                    Locale: locale),
            };
        }

        return new ExportDataSourceContext(
            new ExportDataSourceRequest(format, typeof(SubmissionExportRow), null),
            TenantId,
            FormId,
            options,
            exportPageSize);
    }

    private static async IAsyncEnumerable<FlattenedExportRow> StreamRows(params FlattenedExportRow[] rows)
    {
        foreach (FlattenedExportRow row in rows)
        {
            yield return row;
        }

        await Task.CompletedTask;
    }
}
