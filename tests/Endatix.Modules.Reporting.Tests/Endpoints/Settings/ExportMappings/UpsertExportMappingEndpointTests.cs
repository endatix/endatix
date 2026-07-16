using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Endpoints.Settings.ExportMappings;
using Endatix.Modules.Reporting.Features.ExportMappings;
using Endatix.Modules.Reporting.Tests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Settings.ExportMappings;

public sealed class UpsertExportMappingEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly Upsert _endpoint;

    public UpsertExportMappingEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<Upsert>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsMapping()
    {
        const long exportFormatId = 1;
        UpsertExportMappingEndpointRequest request = new()
        {
            ExportFormatId = exportFormatId,
            SurveyTypeId = null,
            IsDefault = true,
        };

        ExportMappingDto mapping = new(
            Id: 1,
            ExportFormatId: exportFormatId,
            SurveyTypeId: null,
            IsDefault: true,
            ExportFormat: null);

        _mediator.Send(Arg.Any<UpsertExportMappingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(mapping));

        Results<Ok<ExportMappingDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Ok<ExportMappingDto>? ok = response.Result as Ok<ExportMappingDto>;
        ok.Should().NotBeNull();
        ok!.Value.Should().Be(mapping);

        await _mediator.Received(1).Send(
            Arg.Is<UpsertExportMappingCommand>(cmd =>
                cmd.TenantId == SampleData.TENANT_ID &&
                cmd.Request.ExportFormatId == exportFormatId &&
                cmd.Request.IsDefault),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExportFormatNotFound_ReturnsProblem()
    {
        UpsertExportMappingEndpointRequest request = new()
        {
            ExportFormatId = 999,
        };

        _mediator.Send(Arg.Any<UpsertExportMappingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Export format with ID 999 was not found."));

        Results<Ok<ExportMappingDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
