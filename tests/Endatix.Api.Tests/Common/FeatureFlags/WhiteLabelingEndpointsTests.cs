using Endatix.Api.Common.FeatureFlags;
using FastEndpoints;
using Microsoft.Extensions.Configuration;
using FF = Endatix.Framework.FeatureFlags.FeatureFlags;

namespace Endatix.Api.Tests.Common.FeatureFlags;

public sealed class WhiteLabelingEndpointsTests
{
    [Fact]
    public void IsEnabled_WithoutConfiguration_ReturnsTrue()
    {
        // Act
        var enabled = WhiteLabelingEndpoints.IsEnabled(null);

        // Assert
        enabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void IsEnabled_ReadsTheDeploymentFlag(string? value, bool expected)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"Endatix:FeatureFlags:{FF.WhiteLabeling}"] = value })
            .Build();

        // Act
        var enabled = WhiteLabelingEndpoints.IsEnabled(configuration);

        // Assert
        enabled.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void IsDocumented_HidesWhiteLabelingEndpointsOnlyWhenTheFlagIsOff(bool gated, bool enabled, bool expected)
    {
        // Arrange
        var endpoint = new EndpointDefinition(typeof(object), typeof(EmptyRequest), typeof(object));
        if (gated)
        {
            endpoint.Tags(FF.WhiteLabeling);
        }

        // Act
        var documented = WhiteLabelingEndpoints.IsDocumented(endpoint, enabled);

        // Assert
        documented.Should().Be(expected);
    }
}
