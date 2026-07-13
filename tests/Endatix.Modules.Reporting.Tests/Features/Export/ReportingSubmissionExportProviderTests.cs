using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
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
}
