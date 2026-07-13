using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ReportingSubmissionExportProviderTests
{
    private const long TenantId = 1;
    private const long FormId = 100;

    [Fact]
    public async Task PrepareSubmissionExportAsync_WithMissingSchema_ReturnsMissingSchemaMessage()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        var result = await provider.PrepareSubmissionExportAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Save or publish the form definition", StringComparison.Ordinal));
        await reportingExportRepository.DidNotReceive()
            .HasExportableRowsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepareSubmissionExportAsync_WithInvalidSchemaArtifacts_ReturnsInvalidArtifactsMessage()
    {
        FormSchemaEntity schema = new(TenantId, FormId, 1, flatteningMap: " ", codebook: " ");
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        var result = await provider.PrepareSubmissionExportAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("schema artifacts are incomplete or invalid", StringComparison.OrdinalIgnoreCase));
        await reportingExportRepository.DidNotReceive()
            .HasExportableRowsAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepareSubmissionExportAsync_WithNoExportableRows_ReturnsMissingRowsMessage()
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

        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        var result = await provider.PrepareSubmissionExportAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Run admin backfill", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareSubmissionExportAsync_WithSchemaAndRows_ReturnsColumnPlan()
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

        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        var result = await provider.PrepareSubmissionExportAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Columns.Should().NotBeEmpty();
        result.Value.Columns.Should().Contain(column => column.CanonicalKey == nameof(SubmissionExportRow.FormId));
    }

    [Fact]
    public async Task GenerateReportingCodebookJsonAsync_WithMissingSchema_ReturnsMissingSchemaMessage()
    {
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        var result = await provider.GenerateReportingCodebookJsonAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("Save or publish the form definition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateReportingCodebookJsonAsync_WithInvalidSchemaArtifacts_ReturnsInvalidArtifactsMessage()
    {
        FormSchemaEntity schema = new(TenantId, FormId, 1, flatteningMap: " ", codebook: " ");
        IFormSchemaRepository formSchemaRepository = Substitute.For<IFormSchemaRepository>();
        formSchemaRepository
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        var result = await provider.GenerateReportingCodebookJsonAsync(TenantId, FormId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Contains("schema artifacts are incomplete or invalid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateReportingCodebookJsonAsync_WithValidSchema_ReturnsShojiCodebookFromArtifacts()
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

        IReportingExportRepository reportingExportRepository = Substitute.For<IReportingExportRepository>();
        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        Result<string> result = await provider.GenerateReportingCodebookJsonAsync(
            TenantId,
            FormId,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        using JsonDocument actualDocument = JsonDocument.Parse(result.Value);
        using JsonDocument expectedDocument = JsonDocument.Parse(expectedCodebookJson);
        FormSchemaFixtureAssertions.AssertJsonMatchesExpected(
            actualDocument.RootElement,
            expectedDocument.RootElement,
            because: "provider should generate Shoji codebook from persisted schema artifacts");
        await formSchemaRepository.Received(1)
            .GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamSubmissionExportRowsAsync_ForwardsQueryAndMapsRows()
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

        ReportingSubmissionExportProvider provider = new(formSchemaRepository, reportingExportRepository);

        List<SubmissionExportRow> rows = [];
        await foreach (SubmissionExportRow row in provider.StreamSubmissionExportRowsAsync(
                           TenantId,
                           FormId,
                           exportPageSize,
                           TestContext.Current.CancellationToken))
        {
            rows.Add(row);
        }

        rows.Should().ContainSingle();
        SubmissionExportRow mappedRow = rows[0];
        mappedRow.Id.Should().Be(sourceRow.SubmissionId);
        mappedRow.FormId.Should().Be(sourceRow.FormId);
        mappedRow.IsComplete.Should().Be(sourceRow.IsComplete);
        mappedRow.CreatedAt.Should().Be(sourceRow.CreatedAt);
        mappedRow.ModifiedAt.Should().Be(sourceRow.ModifiedAt);
        mappedRow.CompletedAt.Should().Be(sourceRow.CompletedAt);
        mappedRow.SubmitterId.Should().Be(sourceRow.SubmitterId);
        mappedRow.SubmitterDisplayId.Should().Be(sourceRow.SubmitterDisplayId);
        mappedRow.AnswersModel.Should().Be(sourceRow.DataJson);

        reportingExportRepository.Received(1)
            .StreamFlattenedSubmissionsAsync(
                TenantId,
                FormId,
                Arg.Is<ExportQueryOptions>(options => options.PageSize == exportPageSize),
                Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<FlattenedExportRow> StreamRows(
        params FlattenedExportRow[] rows)
    {
        foreach (FlattenedExportRow row in rows)
        {
            yield return row;
        }
    }
}
