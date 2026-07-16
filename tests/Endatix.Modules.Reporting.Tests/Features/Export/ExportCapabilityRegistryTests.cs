using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Capabilities;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportCapabilityRegistryTests
{
    private readonly IExportCapabilityRegistry _registry = new ExportCapabilityRegistry();

    [Theory]
    [InlineData(ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Native, "csv")]
    [InlineData(ExportTarget.Submissions, ExportDeliveryFormat.Json, ExportProfile.Native, "json")]
    [InlineData(ExportTarget.Codebook, ExportDeliveryFormat.Json, ExportProfile.Native, "codebook")]
    [InlineData(ExportTarget.Codebook, ExportDeliveryFormat.Json, ExportProfile.Shoji, "codebook-shoji")]
    public void ToWireKey_ReturnsExpectedWireKey(
        ExportTarget target,
        ExportDeliveryFormat deliveryFormat,
        ExportProfile profile,
        string expectedWireKey)
    {
        _registry.ToWireKey(target, deliveryFormat, profile).Should().Be(expectedWireKey);
    }

    [Fact]
    public void TryGetByWireKey_RoundTripsAllCapabilities()
    {
        foreach (ExportCapability capability in _registry.GetAll())
        {
            _registry.TryGetByWireKey(capability.WireKey, out ExportCapability resolved).Should().BeTrue();
            resolved.Should().Be(capability);
        }
    }

    [Fact]
    public void IsValid_RejectsUnsupportedCombinations()
    {
        _registry.IsValid(ExportTarget.Submissions, ExportDeliveryFormat.Csv, ExportProfile.Shoji).Should().BeFalse();
        _registry.IsValid(ExportTarget.Codebook, ExportDeliveryFormat.Csv, ExportProfile.Native).Should().BeFalse();
    }
}
