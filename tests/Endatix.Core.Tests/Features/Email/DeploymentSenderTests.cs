using Endatix.Core.Branding;
using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using FluentAssertions;

namespace Endatix.Core.Tests.Features.Email;

public sealed class DeploymentSenderTests
{
    [Fact]
    public void From_DefaultSettings_SendsAsNoreplyNamedAfterTheBrand()
    {
        // Act
        var sender = DeploymentSender.From(new EmailSenderSettings(), DeploymentBrand.BuiltIn);

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@endatix.com", "Endatix"));
    }

    [Fact]
    public void From_ConfiguredSender_UsesItOverTheBrandName()
    {
        // Arrange
        var settings = new EmailSenderSettings { Address = "noreply@example.com", Name = "Example Team" };

        // Act
        var sender = DeploymentSender.From(settings, DeploymentBrand.BuiltIn with { ProductName = "Example Forms" });

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@example.com", "Example Team"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("  ", null)]
    public void From_BlankSettings_FallsBackToTheDefaultAddressAndTheBrandName(string address, string? name)
    {
        // Arrange
        var settings = new EmailSenderSettings { Address = address, Name = name };

        // Act
        var sender = DeploymentSender.From(settings, DeploymentBrand.BuiltIn with { ProductName = "Example Forms" });

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@endatix.com", "Example Forms"));
    }
}
