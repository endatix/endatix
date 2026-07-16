using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Endpoints.Tenant.ExportFormats;
using Endatix.Modules.Reporting.Features.ExportFormats;
using Endatix.Modules.Reporting.Tests;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Tenant.ExportFormats;

public sealed class ListExportFormatsEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly List _endpoint;

    public ListExportFormatsEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<List>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsFormats()
    {
        List<ExportFormatDto> formats =
        [
            new(1, "CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native, "csv", "CSV", null, ExportFormatSettings.Default, DateTime.UtcNow, null),
        ];

        _mediator.Send(Arg.Any<ListExportFormatsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(formats));

        Results<Ok<List<ExportFormatDto>>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        Ok<List<ExportFormatDto>>? ok = response.Result as Ok<List<ExportFormatDto>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(1);

        await _mediator.Received(1).Send(
            Arg.Is<ListExportFormatsQuery>(query => query.TenantId == SampleData.TENANT_ID),
            Arg.Any<CancellationToken>());
    }
}
