using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Submissions;
using Endatix.Core.UseCases.Submissions.Export;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Contracts.Export;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Api.Tests.Endpoints.Submissions;

public class ExportTests
{
    private readonly IMediator _mediator;
    private readonly IExporterFactory _exporterFactory;
    private readonly IFormsRepository _formsRepository;
    private readonly IRepository<TenantSettingsEntity> _tenantSettingsRepository;
    private readonly IExportFormatRepository _exportFormatRepository;
    private readonly IExportCapabilityRegistry _exportCapabilityRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<Export> _logger;
    private readonly Export _endpoint;
    private readonly Export _reportingEndpoint;

    public ExportTests()
    {
        _mediator = Substitute.For<IMediator>();
        _exporterFactory = Substitute.For<IExporterFactory>();
        _formsRepository = Substitute.For<IFormsRepository>();
        _tenantSettingsRepository = Substitute.For<IRepository<TenantSettingsEntity>>();
        _exportFormatRepository = Substitute.For<IExportFormatRepository>();
        _exportCapabilityRegistry = CreateCapabilityRegistry();
        _tenantContext = Substitute.For<ITenantContext>();
        _logger = Substitute.For<ILogger<Export>>();
        _endpoint = Factory.Create<Export>(
            _mediator,
            _exporterFactory,
            _formsRepository,
            _tenantSettingsRepository,
            _tenantContext,
            _logger,
            null,
            null);
        _reportingEndpoint = Factory.Create<Export>(
            _mediator,
            _exporterFactory,
            _formsRepository,
            _tenantSettingsRepository,
            _tenantContext,
            _logger,
            _exportFormatRepository,
            _exportCapabilityRegistry);
    }

    private static IExportCapabilityRegistry CreateCapabilityRegistry()
    {
        IExportCapabilityRegistry registry = Substitute.For<IExportCapabilityRegistry>();

        ExportCapability csv = new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Csv,
            ExportProfile.Native,
            "csv",
            "CSV",
            typeof(SubmissionExportRow).FullName!,
            "Tabular CSV export with one row per submission.");
        ExportCapability json = new(
            ExportTarget.Submissions,
            ExportDeliveryFormat.Json,
            ExportProfile.Native,
            "json",
            "JSON",
            typeof(SubmissionExportRow).FullName!,
            "Tabular JSON export with one object per submission.");

        registry.TryGetByWireKey("csv", out Arg.Any<ExportCapability>())
            .Returns(callInfo =>
            {
                callInfo[1] = csv;
                return true;
            });
        registry.TryGetByWireKey("json", out Arg.Any<ExportCapability>())
            .Returns(callInfo =>
            {
                callInfo[1] = json;
                return true;
            });

        return registry;
    }

    private static ExportFormatRecord CreateCsvExportFormatRecord(long id, string? settingsJson = null) =>
        new(id, "CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native, "csv", settingsJson);

    [Fact]
    public async Task HandleAsync_FormNotFound_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var request = new ExportRequest { FormId = formId, ExportFormat = "csv" };

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns((Form?)null);

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportId_Success()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);
        var exportConfig = new CustomExportConfiguration
        {
            Id = exportId,
            Name = "Test Export",
            Format = "csv",
            SqlFunctionName = "custom_export",
            ItemTypeName = typeof(SubmissionExportRow).FullName
        };
        tenantSettings.CustomExports.Add(exportConfig);

        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _endpoint.HttpContext.Response.ContentType.Should().Be("text/csv");
        await _mediator.Received(1).Send(
            Arg.Is<SubmissionsExportQuery>(q =>
                q.FormId == formId &&
                q.Exporter == exporter &&
                q.SqlFunctionName == "custom_export"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithExportId_TenantSettingsNotFound_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((TenantSettingsEntity?)null);

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportId_ExportConfigNotFound_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportId_FormatNotSpecified_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);
        var exportConfig = new CustomExportConfiguration
        {
            Id = exportId,
            Name = "Test Export",
            Format = null, // Format not specified
            SqlFunctionName = "custom_export"
        };
        tenantSettings.CustomExports.Add(exportConfig);

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportId_InvalidItemTypeName_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);
        var exportConfig = new CustomExportConfiguration
        {
            Id = exportId,
            Name = "Test Export",
            Format = "csv",
            SqlFunctionName = "custom_export",
            ItemTypeName = "NonExistent.Type.Name"
        };
        tenantSettings.CustomExports.Add(exportConfig);

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportId_ItemTypeDoesNotImplementIExportItem_ReturnsBadRequest()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);
        var exportConfig = new CustomExportConfiguration
        {
            Id = exportId,
            Name = "Test Export",
            Format = "csv",
            SqlFunctionName = "custom_export",
            ItemTypeName = typeof(Form).FullName // Form doesn't implement IExportItem
        };
        tenantSettings.CustomExports.Add(exportConfig);

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportFormat_RequiresExportFormatIdWhenReportingEnabled()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormat = "json" };
        var form = new Form(tenantId, "Test Form") { Id = formId };

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>()).Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetTenantDefaultAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((ExportFormatRecord?)null);

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithExportFormatId_Success()
    {
        var formId = 1L;
        var exportFormatId = 200L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest
        {
            FormId = formId,
            ExportFormatId = exportFormatId,
            IncludeTestSubmissions = true,
            ColumnScope = ["q1"],
        };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetByIdAsync(tenantId, exportFormatId, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(exportFormatId, """{"aliasProfile":"crunch"}"""));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        await _mediator.Received(1).Send(
            Arg.Is<SubmissionsExportQuery>(q =>
                q.FormId == formId &&
                q.Options.Metadata!.ContainsKey(SubmissionExportMetadataKeys.ExecutionSettings) &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).ExportFormatId == exportFormatId &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).SettingsJson == """{"aliasProfile":"crunch"}""" &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).IncludeTestSubmissions == true &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).ColumnScope!.SequenceEqual(new[] { "q1" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithExportFormatId_NotFound_ReturnsBadRequest()
    {
        var formId = 1L;
        var exportFormatId = 200L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormatId = exportFormatId };

        var form = new Form(tenantId, "Test Form") { Id = formId };

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetByIdAsync(tenantId, exportFormatId, Arg.Any<CancellationToken>())
            .Returns((ExportFormatRecord?)null);

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_WithDefaultFormat_Success()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetTenantDefaultAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(100L));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _reportingEndpoint.HttpContext.Response.ContentType.Should().Be("text/csv");
        await _mediator.Received(1).Send(
            Arg.Is<SubmissionsExportQuery>(q =>
                q.FormId == formId &&
                q.Exporter == exporter &&
                q.SqlFunctionName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithDefaultFormat_ForwardsIncludeTestSubmissionsAndColumnScope()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest
        {
            FormId = formId,
            IncludeTestSubmissions = false,
            ColumnScope = ["q1"],
        };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetTenantDefaultAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(100L));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        await _mediator.Received(1).Send(
            Arg.Is<SubmissionsExportQuery>(q =>
                q.FormId == formId &&
                q.Options.Metadata!.ContainsKey(SubmissionExportMetadataKeys.ExecutionSettings) &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).ExportFormatId == 100L &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).IncludeTestSubmissions == false &&
                ((SubmissionExportExecutionSettings)q.Options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings]).ColumnScope!.SequenceEqual(new[] { "q1" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UnsupportedExportFormat_ReturnsBadRequest()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormat = "unsupported" };

        var form = new Form(tenantId, "Test Form") { Id = formId };

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetTenantDefaultAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((ExportFormatRecord?)null);

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleAsync_GetHeadersAsyncFails_ReturnsInternalServerError()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormatId = 100L };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetByIdAsync(tenantId, 100L, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(100L));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileExport>.Error("Failed to get headers"));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task HandleAsync_ExportHandlerFails_ReturnsInternalServerError()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormatId = 100L };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetByIdAsync(tenantId, 100L, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(100L));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileExport>.Error("Export failed"));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task HandleAsync_WithExportId_DefaultItemTypeName_UsesSubmissionExportRow()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);
        var exportConfig = new CustomExportConfiguration
        {
            Id = exportId,
            Name = "Test Export",
            Format = "csv",
            SqlFunctionName = "custom_export",
            ItemTypeName = null // Should default to SubmissionExportRow
        };
        tenantSettings.CustomExports.Add(exportConfig);

        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _exporterFactory.Received(1).GetExporter("csv", typeof(SubmissionExportRow));
    }

    [Fact]
    public async Task HandleAsync_WithExportId_DynamicExportRow_Success()
    {
        // Arrange
        var formId = 1L;
        var exportId = 100L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportId = exportId };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var tenantSettings = new TenantSettingsEntity(tenantId);
        var exportConfig = new CustomExportConfiguration
        {
            Id = exportId,
            Name = "Test Export",
            Format = "codebook",
            SqlFunctionName = "export_codebook",
            ItemTypeName = typeof(DynamicExportRow).FullName
        };
        tenantSettings.CustomExports.Add(exportConfig);

        var exporter = CreateMockExporter("codebook", typeof(DynamicExportRow));
        var fileExport = new FileExport("application/json", "codebook-1.json");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);
        _exporterFactory.GetExporter("codebook", typeof(DynamicExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _endpoint.HttpContext.Response.ContentType.Should().Be("application/json");
        await _mediator.Received(1).Send(
            Arg.Is<SubmissionsExportQuery>(q =>
                q.FormId == formId &&
                q.Exporter == exporter &&
                q.SqlFunctionName == "export_codebook"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var formId = 1L;
        var request = new ExportRequest { FormId = formId, ExportFormat = "csv" };

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Form?>(new Exception("Unexpected error")));

        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task HandleAsync_SetsCorrectResponseHeaders()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormatId = 100L };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetByIdAsync(tenantId, 100L, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(100L));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        _reportingEndpoint.HttpContext.Response.ContentType.Should().Be("text/csv");
        _reportingEndpoint.HttpContext.Response.Headers.ContentDisposition.Should().Contain("attachment; filename=submissions-1.csv");
    }

    [Fact]
    public async Task HandleAsync_IncludesFormIdInMetadata()
    {
        var formId = 1L;
        var tenantId = SampleData.TENANT_ID;
        var request = new ExportRequest { FormId = formId, ExportFormatId = 100L };

        var form = new Form(tenantId, "Test Form") { Id = formId };
        var exporter = CreateMockExporter("csv", typeof(SubmissionExportRow));
        var fileExport = new FileExport("text/csv", "submissions-1.csv");

        _formsRepository.GetByIdAsync(formId, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantContext.TenantId.Returns(tenantId);
        _exportFormatRepository.GetByIdAsync(tenantId, 100L, Arg.Any<CancellationToken>())
            .Returns(CreateCsvExportFormatRecord(100L));
        _exporterFactory.GetExporter("csv", typeof(SubmissionExportRow))
            .Returns(exporter);
        exporter.GetHeadersAsync(Arg.Any<ExportOptions>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));
        _mediator.Send(Arg.Any<SubmissionsExportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(fileExport));

        await _reportingEndpoint.HandleAsync(request, CancellationToken.None);

        // Assert
        await exporter.Received(1).GetHeadersAsync(
            Arg.Is<ExportOptions>(o =>
                o.Metadata != null &&
                o.Metadata.ContainsKey("FormId") &&
                o.Metadata["FormId"].Equals(formId)),
            Arg.Any<CancellationToken>());
    }

    private static IExporter CreateMockExporter(string format, Type itemType)
    {
        var exporter = Substitute.For<IExporter>();
        exporter.Format.Returns(format);
        exporter.ItemType.Returns(itemType);
        return exporter;
    }
}
