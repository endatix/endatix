using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Tests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Settings.ExportFormats;

public sealed class GetExportFormatEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly Get _endpoint;

    public GetExportFormatEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<Get>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsFormat()
    {
        const long exportFormatId = 1;
        GetExportFormatRequest request = new() { ExportFormatId = exportFormatId };

        ExportFormatDto format = new(
            exportFormatId, "CSV Export", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native,
            "csv", "CSV", null, ExportFormatSettings.Default, DateTime.UtcNow, null,
            AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilterSets.Submissions));

        _mediator.Send(Arg.Any<GetExportFormatQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(format));

        Results<Ok<ExportFormatDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Ok<ExportFormatDto>? ok = response.Result as Ok<ExportFormatDto>;
        ok.Should().NotBeNull();
        ok!.Value.Should().Be(format);

        await _mediator.Received(1).Send(
            Arg.Is<GetExportFormatQuery>(query =>
                query.TenantId == SampleData.TENANT_ID &&
                query.ExportFormatId == exportFormatId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsProblem()
    {
        const long exportFormatId = 999;
        GetExportFormatRequest request = new() { ExportFormatId = exportFormatId };

        _mediator.Send(Arg.Any<GetExportFormatQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Export format not found."));

        Results<Ok<ExportFormatDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
