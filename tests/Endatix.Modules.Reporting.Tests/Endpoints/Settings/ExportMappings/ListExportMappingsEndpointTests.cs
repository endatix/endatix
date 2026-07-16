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

public sealed class ListExportMappingsEndpointTests
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly Endatix.Modules.Reporting.Endpoints.Settings.ExportMappings.List _endpoint;

    public ListExportMappingsEndpointTests()
    {
        _mediator = Substitute.For<IMediator>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _endpoint = Factory.Create<Endatix.Modules.Reporting.Endpoints.Settings.ExportMappings.List>(_mediator, _tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_ReturnsMappings()
    {
        List<ExportMappingDto> mappings =
        [
            new(Id: 1, ExportFormatId: 1, SurveyTypeId: null, IsDefault: true, ExportFormat: null),
            new(Id: 2, ExportFormatId: 2, SurveyTypeId: 100, IsDefault: false, ExportFormat: null),
        ];

        _mediator.Send(Arg.Any<ListExportMappingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(mappings));

        Results<Ok<List<ExportMappingDto>>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        Ok<List<ExportMappingDto>>? ok = response.Result as Ok<List<ExportMappingDto>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().HaveCount(2);

        await _mediator.Received(1).Send(
            Arg.Is<ListExportMappingsQuery>(query => query.TenantId == SampleData.TENANT_ID),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmpty_ReturnsEmptyList()
    {
        _mediator.Send(Arg.Any<ListExportMappingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new List<ExportMappingDto>()));

        Results<Ok<List<ExportMappingDto>>, ProblemHttpResult> response =
            await _endpoint.ExecuteAsync(TestContext.Current.CancellationToken);

        Ok<List<ExportMappingDto>>? ok = response.Result as Ok<List<ExportMappingDto>>;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEmpty();
    }
}
