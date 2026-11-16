using Endatix.Core.Branding;
using FluentAssertions;

namespace Endatix.Core.Tests.Branding;

public sealed class DeploymentBrandTests
{
    [Fact]
    public void TemplateVariables_BuiltInBrand_RenderTheTextTheSeedRowsShippedWith()
    {
        // Act
        var variables = DeploymentBrand.BuiltIn.TemplateVariables;

        // Assert
        variables.Should().Equal(new Dictionary<string, string>
        {
            ["productName"] = "Endatix",
            ["supportAddress"] = "support@endatix.com",
            ["websiteUrl"] = "https://endatix.com",
            ["senderName"] = "Endatix",
        });
    }

    [Fact]
    public void TemplateVariables_PackBrand_UseThePackValues()
    {
        // Arrange
        var brand = new DeploymentBrand(
            ProductName: "Example Forms",
            AppName: "Example Forms",
            AssistantName: "Example Assistant",
            WebsiteUrl: "https://example.com",
            Email: new BrandEmail("Example Team", "noreply@example.com", "help@example.com"));

        // Act
        var variables = brand.TemplateVariables;

        // Assert
        variables.Should().Equal(new Dictionary<string, string>
        {
            ["productName"] = "Example Forms",
            ["supportAddress"] = "help@example.com",
            ["websiteUrl"] = "https://example.com",
            ["senderName"] = "Example Team",
        });
    }

    [Fact]
    public void BuiltIn_HasNoSenderNameOrFromAddress_SoProvidersSendWhatTheyDidBefore()
    {
        // Act
        var email = DeploymentBrand.BuiltIn.Email;

        // Assert
        email.SenderName.Should().BeNull();
        email.FromAddress.Should().BeNull();
    }
}
