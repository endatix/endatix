using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Endpoints.Settings.ExportNamingConventions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Settings.ExportNamingConventions;

public sealed class ListExportNamingConventionsEndpointTests
{
    private readonly IColumnAliasTransformerRegistry _registry;
    private readonly List _endpoint;

    public ListExportNamingConventionsEndpointTests()
    {
        _registry = Substitute.For<IColumnAliasTransformerRegistry>();
        _endpoint = Factory.Create<List>(_registry);
    }

    [Fact]
    public async Task HandleAsync_ReturnsConventionsFromRegistry()
    {
        List<ColumnAliasNamingConventionDto> conventions =
        [
            new("native", "Native", "Canonical storage keys", "question1"),
            new("crunch", "Crunch", "Sequential question-index aliases", "Q1"),
        ];
        _registry.GetCatalog().Returns(conventions);

        await _endpoint.HandleAsync(TestContext.Current.CancellationToken);

        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _registry.Received(1).GetCatalog();
    }

    [Fact]
    public async Task HandleAsync_WhenRegistryEmpty_ReturnsEmptyList()
    {
        _registry.GetCatalog().Returns([]);

        await _endpoint.HandleAsync(TestContext.Current.CancellationToken);

        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _registry.Received(1).GetCatalog();
    }
}
