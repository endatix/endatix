using Endatix.Core.Branding;
using Endatix.Core.Features.Email;
using FluentAssertions;

namespace Endatix.Core.Tests.Features.Email;

public sealed class EmailVariablesTests
{
    private static readonly DeploymentBrand _exampleBrand = new(
        ProductName: "Example Forms",
        AppName: "Example Forms",
        AssistantName: "Example Assistant",
        WebsiteUrl: "https://example.com",
        SupportAddress: "help@example.com");

    [Fact]
    public void Values_BuiltInBrand_RenderTheTextTheSeedRowsShippedWith()
    {
        // Act
        var values = EmailVariables.From(DeploymentBrand.BuiltIn).Values;

        // Assert
        values.Should().Equal(new Dictionary<string, string>
        {
            ["productName"] = "Endatix",
            ["supportAddress"] = "support@endatix.com",
            ["websiteUrl"] = "https://endatix.com",
        });
    }

    [Fact]
    public void From_PackBrand_TakesItsIdentity()
    {
        // Act
        var variables = EmailVariables.From(_exampleBrand);

        // Assert
        variables.Should().Be(new EmailVariables("Example Forms", "Example Forms", "help@example.com", "https://example.com"));
    }

    [Fact]
    public void ForTemplate_MetadataClash_TheMetadataWinsAsText()
    {
        // Arrange
        var variables = EmailVariables.From(_exampleBrand);

        // Act
        var merged = variables.ForTemplate(new Dictionary<string, object> { ["productName"] = "Override", ["count"] = 3 });

        // Assert
        merged.Should().Contain("productName", "Override").And.Contain("count", "3").And.Contain("websiteUrl", "https://example.com");
    }

    [Fact]
    public void ForProvider_MetadataClash_TheMetadataWinsAndKeepsItsType()
    {
        // Arrange
        var variables = EmailVariables.From(_exampleBrand);
        var nested = new { plan = "pro" };

        // Act
        var data = variables.ForProvider(new Dictionary<string, object>
        {
            ["productName"] = "Override",
            ["count"] = 3,
            ["isTrial"] = true,
            ["account"] = nested,
        });

        // Assert
        data.Should().Equal(new Dictionary<string, object>
        {
            ["productName"] = "Override",
            ["count"] = 3,
            ["isTrial"] = true,
            ["account"] = nested,
            ["supportAddress"] = "help@example.com",
            ["websiteUrl"] = "https://example.com",
        });
    }

    [Fact]
    public void ForProvider_NoMetadata_GivesTheVariables()
    {
        // Act
        var data = EmailVariables.From(DeploymentBrand.BuiltIn).ForProvider(null);

        // Assert
        data.Keys.Should().BeEquivalentTo("productName", "supportAddress", "websiteUrl");
    }
}
