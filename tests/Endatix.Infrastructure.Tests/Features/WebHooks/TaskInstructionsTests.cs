using Endatix.Infrastructure.Features.WebHooks;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class TaskInstructionsTests
{
    [Fact]
    public void Constructor_WithUri_ShouldInitializeCorrectly()
    {
        // Arrange
        var uri = "https://api.example.com/webhooks";

        // Act
        var instructions = new TaskInstructions(uri);

        // Assert
        instructions.Uri.Should().Be(uri);
        instructions.Authentication.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithUriAndAuthentication_ShouldInitializeCorrectly()
    {
        // Arrange
        var uri = "https://api.example.com/webhooks";
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
            ApiKeyHeader = "X-API-KEY"
        };

        // Act
        var instructions = new TaskInstructions(uri, auth);

        // Assert
        instructions.Uri.Should().Be(uri);
        instructions.Authentication.Should().Be(auth);
        instructions.Authentication!.Type.Should().Be(AuthenticationType.ApiKey);
        instructions.Authentication!.ApiKey.Should().Be("test-key");
        instructions.Authentication!.ApiKeyHeader.Should().Be("X-API-KEY");
    }

    [Fact]
    public void Constructor_WithUriAndNullAuthentication_ShouldInitializeCorrectly()
    {
        // Arrange
        var uri = "https://api.example.com/webhooks";

        // Act
        var instructions = new TaskInstructions(uri, null);

        // Assert
        instructions.Uri.Should().Be(uri);
        instructions.Authentication.Should().BeNull();
    }

    [Fact]
    public void FromEndpoint_WithEndpointWithoutAuthentication_ShouldCreateCorrectInstructions()
    {
        // Arrange
        var endpoint = new WebHookEndpoint
        {
            Url = "https://api.example.com/webhooks"
        };

        // Act
        var instructions = TaskInstructions.FromEndpoint(endpoint);

        // Assert
        instructions.Uri.Should().Be("https://api.example.com/webhooks");
        instructions.Authentication.Should().BeNull();
    }

    [Fact]
    public void FromEndpoint_WithEndpointWithAuthentication_ShouldCreateCorrectInstructions()
    {
        // Arrange
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "secret-key",
            ApiKeyHeader = "X-SURVEY-API-KEY"
        };

        var endpoint = new WebHookEndpoint
        {
            Url = "https://api.example.com/webhooks",
            Authentication = auth
        };

        // Act
        var instructions = TaskInstructions.FromEndpoint(endpoint);

        // Assert
        instructions.Uri.Should().Be("https://api.example.com/webhooks");
        instructions.Authentication.Should().Be(auth);
        instructions.Authentication!.Type.Should().Be(AuthenticationType.ApiKey);
        instructions.Authentication!.ApiKey.Should().Be("secret-key");
        instructions.Authentication!.ApiKeyHeader.Should().Be("X-SURVEY-API-KEY");
    }

    [Fact]
    public void FromEndpoint_WithEndpointWithNoneAuthentication_ShouldCreateCorrectInstructions()
    {
        // Arrange
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.None
        };

        var endpoint = new WebHookEndpoint
        {
            Url = "https://api.example.com/webhooks",
            Authentication = auth
        };

        // Act
        var instructions = TaskInstructions.FromEndpoint(endpoint);

        // Assert
        instructions.Uri.Should().Be("https://api.example.com/webhooks");
        instructions.Authentication.Should().Be(auth);
        instructions.Authentication!.Type.Should().Be(AuthenticationType.None);
        instructions.Authentication!.ApiKey.Should().BeNull();
        instructions.Authentication!.ApiKeyHeader.Should().BeNull();
    }

    [Theory]
    [InlineData("https://api.example.com/webhooks")]
    [InlineData("http://localhost:3000/webhooks")]
    [InlineData("https://webhook.site/unique-id")]
    public void Constructor_WithVariousUris_ShouldInitializeCorrectly(string uri)
    {
        // Act
        var instructions = new TaskInstructions(uri);

        // Assert
        instructions.Uri.Should().Be(uri);
        instructions.Authentication.Should().BeNull();
    }

    [Fact]
    public void FromEndpoint_WithComplexEndpoint_ShouldPreserveAllProperties()
    {
        // Arrange
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "complex-secret-key-123",
            ApiKeyHeader = "X-CUSTOM-AUTH-HEADER"
        };

        var endpoint = new WebHookEndpoint
        {
            Url = "https://complex-api.example.com/v2/webhooks/events",
            Authentication = auth
        };

        // Act
        var instructions = TaskInstructions.FromEndpoint(endpoint);

        // Assert
        instructions.Uri.Should().Be("https://complex-api.example.com/v2/webhooks/events");
        instructions.Authentication.Should().NotBeNull();
        instructions.Authentication!.Type.Should().Be(AuthenticationType.ApiKey);
        instructions.Authentication!.ApiKey.Should().Be("complex-secret-key-123");
        instructions.Authentication!.ApiKeyHeader.Should().Be("X-CUSTOM-AUTH-HEADER");
    }
} 