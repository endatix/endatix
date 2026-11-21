using Endatix.Core.Branding;
using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using FluentAssertions;

namespace Endatix.Core.Tests.Features.Email;

public sealed class DeploymentSenderTests
{
    private static readonly DeploymentBrand _packBrand = DeploymentBrand.BuiltIn with { ProductName = "Example Forms" };

    [Fact]
    public void From_DefaultSettings_SendsAsNoreplyNamedAfterTheBrandWithNoConfiguredAddress()
    {
        // Act
        var sender = DeploymentSender.From(new EmailSenderSettings(), DeploymentBrand.BuiltIn);

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@endatix.com", "Endatix", ConfiguredAddress: null));
    }

    [Fact]
    public void From_ConfiguredSender_IsTheConfiguredAddressAndName()
    {
        // Arrange
        var settings = new EmailSenderSettings { Address = "noreply@example.com", Name = "Example Team" };

        // Act
        var sender = DeploymentSender.From(settings, _packBrand);

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@example.com", "Example Team", "noreply@example.com"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("  ", null)]
    public void From_BlankSettings_FallsBackToTheDefaultAddressAndTheBrandName(string address, string? name)
    {
        // Arrange
        var settings = new EmailSenderSettings { Address = address, Name = name };

        // Act
        var sender = DeploymentSender.From(settings, _packBrand);

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@endatix.com", "Example Forms", ConfiguredAddress: null));
    }

    [Fact]
    public void From_OnlyDeprecatedValues_UsesThemButNeverAsAConfiguredAddress()
    {
        // Arrange
        var legacy = new EmailSenderSettings { Address = "smtp@example.com", Name = "SMTP Team" };

        // Act
        var sender = DeploymentSender.From(new EmailSenderSettings(), _packBrand, legacy);

        // Assert
        sender.Should().Be(new DeploymentSender("smtp@example.com", "SMTP Team", ConfiguredAddress: null));
    }

    [Fact]
    public void From_SenderAndDeprecatedValues_TheSenderWins()
    {
        // Arrange
        var settings = new EmailSenderSettings { Address = "noreply@example.com", Name = "Example Team" };
        var legacy = new EmailSenderSettings { Address = "smtp@example.com", Name = "SMTP Team" };

        // Act
        var sender = DeploymentSender.From(settings, _packBrand, legacy);

        // Assert
        sender.Should().Be(new DeploymentSender("noreply@example.com", "Example Team", "noreply@example.com"));
    }
}
