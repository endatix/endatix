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

public sealed class CreateExportFormatEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly Create _endpoint;

    public CreateExportFormatEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<Create>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsCreated()
    {
        var request = new Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats.CreateExportFormatRequest
        {
            Name = "CSV Export",
            ExportTarget = ExportTarget.Submissions,
            DeliveryFormat = ExportDeliveryFormat.Csv,
        };

        ExportFormatDto format = new(
            1, "CSV Export", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native,
            "csv", "CSV", null, ExportFormatSettings.Default, DateTime.UtcNow, null,
            AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilterSets.Submissions));

        _mediator.Send(Arg.Any<CreateExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExportFormatDto>.Created(format));

        Results<Created<ExportFormatDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Created<ExportFormatDto>? created = response.Result as Created<ExportFormatDto>;
        created.Should().NotBeNull();
        created!.Value.Should().Be(format);

        await _mediator.Received(1).Send(
            Arg.Is<CreateExportFormatCommand>(cmd =>
                cmd.TenantId == SampleData.TENANT_ID &&
                cmd.Name == "CSV Export" &&
                cmd.ExportTarget == ExportTarget.Submissions &&
                cmd.DeliveryFormat == ExportDeliveryFormat.Csv &&
                cmd.Profile == ExportProfile.Native),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ReturnsProblem()
    {
        var request = new Endatix.Modules.Reporting.Endpoints.Settings.ExportFormats.CreateExportFormatRequest
        {
            Name = "CSV Export",
            ExportTarget = ExportTarget.Submissions,
            DeliveryFormat = ExportDeliveryFormat.Csv,
        };

        _mediator.Send(Arg.Any<CreateExportFormatCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Invalid(new ValidationError
            {
                Identifier = "Name",
                ErrorMessage = "An export format named 'CSV Export' already exists.",
            }));

        Results<Created<ExportFormatDto>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        ProblemHttpResult? problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
