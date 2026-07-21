using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Capabilities;

public sealed class ExportCapabilityRegistryTests
{
    private readonly ExportCapabilityRegistry _sut = new();

    [Fact]
    public void GetAll_IncludesCapabilitiesFromDataSourceClasses()
    {
        IReadOnlyList<ExportCapability> capabilities = _sut.GetAll();

        capabilities.Select(capability => capability.WireKey).Should().BeEquivalentTo(
            ["csv", "csv-shoji", "json", "codebook", "codebook-shoji"]);
    }

    [Fact]
    public void TryGetByWireKey_ReturnsExpectedAllowedFilters()
    {
        _sut.TryGetByWireKey("codebook", out ExportCapability native).Should().BeTrue();
        native.AllowedFilters.Should().Be(ExportRequestFilters.None);

        _sut.TryGetByWireKey("codebook-shoji", out ExportCapability shoji).Should().BeTrue();
        shoji.AllowedFilters.Should().Be(ExportRequestFilterSets.ShojiCodebook);

        _sut.TryGetByWireKey("csv", out ExportCapability csv).Should().BeTrue();
        csv.AllowedFilters.Should().Be(ExportRequestFilterSets.Submissions);
    }
}
