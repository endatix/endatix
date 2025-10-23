using Endatix.Infrastructure.Features.WebHooks;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class WebHookSettingsTests
{
    [Fact]
    public void WebHookEndpoint_WithValidUrl_ShouldInitializeCorrectly()
    {
        // Arrange
        var url = "https://api.example.com/webhooks";

        // Act
        var endpoint = new WebHookEndpoint { Url = url };

        // Assert
        endpoint.Url.Should().Be(url);
        endpoint.Authentication.Should().BeNull();
    }

    [Fact]
    public void WebHookEndpoint_WithAuthentication_ShouldInitializeCorrectly()
    {
        // Arrange
        var url = "https://api.example.com/webhooks";
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
            ApiKeyHeader = "X-API-KEY"
        };

        // Act
        var endpoint = new WebHookEndpoint { Url = url, Authentication = auth };

        // Assert
        endpoint.Url.Should().Be(url);
        endpoint.Authentication.Should().Be(auth);
        endpoint.Authentication!.Type.Should().Be(AuthenticationType.ApiKey);
        endpoint.Authentication!.ApiKey.Should().Be("test-key");
        endpoint.Authentication!.ApiKeyHeader.Should().Be("X-API-KEY");
    }

    [Fact]
    public void AuthenticationConfig_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var auth = new AuthenticationConfig();

        // Assert
        auth.Type.Should().Be(AuthenticationType.None);
        auth.ApiKey.Should().BeNull();
        auth.ApiKeyHeader.Should().BeNull();
    }

    [Fact]
    public void AuthenticationConfig_ApiKeyType_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "secret-key",
            ApiKeyHeader = "X-SURVEY-API-KEY"
        };

        // Assert
        auth.Type.Should().Be(AuthenticationType.ApiKey);
        auth.ApiKey.Should().Be("secret-key");
        auth.ApiKeyHeader.Should().Be("X-SURVEY-API-KEY");
    }

    [Fact]
    public void EventSetting_GetAllEndpoints_WithOnlyWebHookEndpoints_ReturnsEndpoints()
    {
        // Arrange
        var endpoints = new List<WebHookEndpoint>
        {
            new() { Url = "https://api1.example.com/webhooks" },
            new() { Url = "https://api2.example.com/webhooks" }
        };

        var eventSetting = new WebHookSettings.EventSetting
        {
            EventName = "test_event",
            IsEnabled = true,
            WebHookEndpoints = endpoints
        };

        // Act
        var result = eventSetting.GetAllEndpoints();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Url == "https://api1.example.com/webhooks");
        result.Should().Contain(e => e.Url == "https://api2.example.com/webhooks");
    }

    [Fact]
    public void EventSetting_GetAllEndpoints_WithOnlyWebHookUrls_ReturnsEndpointsFromUrls()
    {
        // Arrange
        var urls = new List<string>
        {
            "https://api1.example.com/webhooks",
            "https://api2.example.com/webhooks"
        };

        var eventSetting = new WebHookSettings.EventSetting
        {
            EventName = "test_event",
            IsEnabled = true,
            WebHookUrls = urls
        };

        // Act
        var result = eventSetting.GetAllEndpoints();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Url == "https://api1.example.com/webhooks" && e.Authentication == null);
        result.Should().Contain(e => e.Url == "https://api2.example.com/webhooks" && e.Authentication == null);
    }

    [Fact]
    public void EventSetting_GetAllEndpoints_WithBothEndpointsAndUrls_ReturnsCombined()
    {
        // Arrange
        var endpoints = new List<WebHookEndpoint>
        {
            new() { 
                Url = "https://secure.example.com/webhooks",
                Authentication = new AuthenticationConfig { Type = AuthenticationType.ApiKey, ApiKey = "key1" }
            }
        };

        var urls = new List<string>
        {
            "https://public.example.com/webhooks"
        };

        var eventSetting = new WebHookSettings.EventSetting
        {
            EventName = "test_event",
            IsEnabled = true,
            WebHookEndpoints = endpoints,
            WebHookUrls = urls
        };

        // Act
        var result = eventSetting.GetAllEndpoints();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Url == "https://secure.example.com/webhooks" && e.Authentication != null);
        result.Should().Contain(e => e.Url == "https://public.example.com/webhooks" && e.Authentication == null);
    }

    [Fact]
    public void EventSetting_GetAllEndpoints_WithEmptyCollections_ReturnsEmpty()
    {
        // Arrange
        var eventSetting = new WebHookSettings.EventSetting
        {
            EventName = "test_event",
            IsEnabled = true,
            WebHookEndpoints = new List<WebHookEndpoint>(),
            WebHookUrls = new List<string>()
        };

        // Act
        var result = eventSetting.GetAllEndpoints();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void EventSetting_GetAllEndpoints_WithNullCollections_ReturnsEmpty()
    {
        // Arrange
        var eventSetting = new WebHookSettings.EventSetting
        {
            EventName = "test_event",
            IsEnabled = true,
            WebHookEndpoints = null,
            WebHookUrls = null
        };

        // Act
        var result = eventSetting.GetAllEndpoints();

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AuthenticationType.None)]
    [InlineData(AuthenticationType.ApiKey)]
    public void AuthenticationType_AllValues_ShouldBeSupported(AuthenticationType type)
    {
        // Arrange & Act
        var auth = new AuthenticationConfig { Type = type };

        // Assert
        auth.Type.Should().Be(type);
    }

    [Fact]
    public void WebHookSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new WebHookSettings();

        // Assert
        settings.ServerSettings.Should().NotBeNull();
        settings.Tenants.Should().NotBeNull();
    }

    [Fact]
    public void HttpServerSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var serverSettings = new WebHookSettings.HttpServerSettings();

        // Assert
        serverSettings.PipelineTimeoutInSeconds.Should().Be(120);
        serverSettings.AttemptTimeoutInSeconds.Should().Be(10);
        serverSettings.RetryAttempts.Should().Be(5);
        serverSettings.Delay.Should().Be(10);
        serverSettings.MaxConcurrentRequests.Should().Be(5);
        serverSettings.MaxQueueSize.Should().Be(25);
    }

    [Fact]
    public void WebHookEvents_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var events = new WebHookSettings.WebHookEvents();

        // Assert
        events.FormCreated.Should().NotBeNull();
        events.FormCreated.IsEnabled.Should().BeFalse();
        events.FormCreated.EventName.Should().Be("form_created");

        events.FormUpdated.Should().NotBeNull();
        events.FormUpdated.IsEnabled.Should().BeFalse();
        events.FormUpdated.EventName.Should().Be("form_updated");

        events.FormEnabledStateChanged.Should().NotBeNull();
        events.FormEnabledStateChanged.IsEnabled.Should().BeFalse();
        events.FormEnabledStateChanged.EventName.Should().Be("form_enabled_state_changed");

        events.SubmissionCompleted.Should().NotBeNull();
        events.SubmissionCompleted.IsEnabled.Should().BeFalse();
        events.SubmissionCompleted.EventName.Should().Be("submission_completed");

        events.FormDeleted.Should().NotBeNull();
        events.FormDeleted.IsEnabled.Should().BeFalse();
        events.FormDeleted.EventName.Should().Be("form_deleted");
    }
} 