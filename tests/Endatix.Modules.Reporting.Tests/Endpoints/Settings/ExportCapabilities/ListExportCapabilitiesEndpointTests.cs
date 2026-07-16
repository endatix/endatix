using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Endpoints.Settings.ExportCapabilities;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Settings.ExportCapabilities;

public sealed class ListExportCapabilitiesEndpointTests
{
    private readonly IExportCapabilityRegistry _registry;
    private readonly List _endpoint;

    public ListExportCapabilitiesEndpointTests()
    {
        _registry = Substitute.For<IExportCapabilityRegistry>();
        _endpoint = Factory.Create<List>(_registry);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCapabilitiesFromRegistry()
    {
        List<ExportCapability> capabilities =
        [
            new(ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native, "csv", "CSV", "type1"),
            new(ExportTarget.Codebook, ExportDeliveryFormat.Json, ExportProfile.Shoji, "codebook-shoji", "Codebook (Shoji)", "type2"),
        ];
        _registry.GetAll().Returns(capabilities);

        await _endpoint.HandleAsync(TestContext.Current.CancellationToken);

        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _registry.Received(1).GetAll();
    }

    [Fact]
    public async Task HandleAsync_WhenRegistryEmpty_ReturnsEmptyList()
    {
        _registry.GetAll().Returns([]);

        await _endpoint.HandleAsync(TestContext.Current.CancellationToken);

        _endpoint.HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _registry.Received(1).GetAll();
    }
}
