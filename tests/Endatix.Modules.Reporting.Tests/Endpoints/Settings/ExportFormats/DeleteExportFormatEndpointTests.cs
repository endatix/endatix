using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Tests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Settings.ExportFormats;

public sealed class DeleteExportFormatEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly Delete _endpoint;

    public DeleteExportFormatEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<Delete>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsOk()
    {
        const long exportFormatId = 1;
        DeleteExportFormatRequest request = new() { ExportFormatId = exportFormatId };

        _mediator.Send(Arg.Any<DeleteExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(exportFormatId.ToString()));

        Results<Ok<string>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Ok<string>? ok = response.Result as Ok<string>;
        ok.Should().NotBeNull();
        ok!.Value.Should().Be(exportFormatId.ToString());

        await _mediator.Received(1).Send(
            Arg.Is<DeleteExportFormatCommand>(cmd =>
                cmd.TenantId == SampleData.TENANT_ID &&
                cmd.ExportFormatId == exportFormatId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotFound_ReturnsProblem()
    {
        const long exportFormatId = 999;
        DeleteExportFormatRequest request = new() { ExportFormatId = exportFormatId };

        _mediator.Send(Arg.Any<DeleteExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.NotFound("Export format not found."));

        Results<Ok<string>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReferencedByMapping_ReturnsProblem()
    {
        const long exportFormatId = 1;
        DeleteExportFormatRequest request = new() { ExportFormatId = exportFormatId };

        _mediator.Send(Arg.Any<DeleteExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(new ValidationError
            {
                Identifier = "ExportFormatId",
                ErrorMessage = "Export format is referenced by an active mapping and cannot be deleted.",
            }));

        Results<Ok<string>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
