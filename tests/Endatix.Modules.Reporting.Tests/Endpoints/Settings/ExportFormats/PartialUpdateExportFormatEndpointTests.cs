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

public sealed class PartialUpdateExportFormatEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly PartialUpdate _endpoint;

    public PartialUpdateExportFormatEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<PartialUpdate>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsUpdatedFormat()
    {
        const long exportFormatId = 1;
        PartialUpdateExportFormatRequest request = new()
        {
            ExportFormatId = exportFormatId,
            Name = "Updated CSV Export",
            Description = "Updated description",
        };

        ExportFormatDto updated = new(
            exportFormatId, "Updated CSV Export", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native,
            "csv", "CSV", "Updated description", ExportFormatSettings.Default, DateTime.UtcNow, DateTime.UtcNow);

        _mediator.Send(Arg.Any<UpdateExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(updated));

        Results<Ok<ExportFormatDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Ok<ExportFormatDto>? ok = response.Result as Ok<ExportFormatDto>;
        ok.Should().NotBeNull();
        ok!.Value.Should().Be(updated);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateExportFormatCommand>(cmd =>
                cmd.TenantId == SampleData.TENANT_ID &&
                cmd.ExportFormatId == exportFormatId &&
                cmd.Name == "Updated CSV Export" &&
                cmd.Description == "Updated description"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsProblem()
    {
        const long exportFormatId = 999;
        PartialUpdateExportFormatRequest request = new() { ExportFormatId = exportFormatId };

        _mediator.Send(Arg.Any<UpdateExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Export format not found."));

        Results<Ok<ExportFormatDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
