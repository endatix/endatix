using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Features.WebHooks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class BackgroundTaskWebHookServiceTests
{
    private readonly ILogger<BackgroundTaskWebHookService> _logger;
    private readonly IBackgroundTasksQueue _backgroundQueue;
    private readonly WebHookServer _webHookServer;
    private readonly IOptions<WebHookSettings> _options;

    public BackgroundTaskWebHookServiceTests()
    {
        _logger = Substitute.For<ILogger<BackgroundTaskWebHookService>>();
        _backgroundQueue = Substitute.For<IBackgroundTasksQueue>();
        
        // WebHookServer requires HttpClient and ILogger<WebHookServer> constructor arguments
        var httpClient = Substitute.For<HttpClient>();
        var webHookLogger = Substitute.For<ILogger<WebHookServer>>();
        _webHookServer = Substitute.For<WebHookServer>(httpClient, webHookLogger);
        
        _options = Substitute.For<IOptions<WebHookSettings>>();
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithDisabledEvent_DoesNotEnqueueTasks()
    {
        // Arrange
        var settings = CreateSettingsWithDisabledEvents();
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_created");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.DidNotReceive().EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventAndEndpoints_EnqueuesTasksForEachEndpoint()
    {
        // Arrange
        var settings = CreateSettingsWithEnabledFormCreated(endpoints: new List<WebHookEndpoint>
        {
            new() { Url = "https://api1.example.com/webhooks" },
            new() { Url = "https://api2.example.com/webhooks" }
        });
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_created");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(2).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventAndUrls_EnqueuesTasksForEachUrl()
    {
        // Arrange
        var settings = CreateSettingsWithEnabledFormUpdated(urls: new List<string>
        {
            "https://api1.example.com/webhooks",
            "https://api2.example.com/webhooks"
        });
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_updated");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(2).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventAndMixedEndpoints_EnqueuesTasksForAll()
    {
        // Arrange
        var endpoints = new List<WebHookEndpoint>
        {
            new() { 
                Url = "https://secure.example.com/webhooks",
                Authentication = new AuthenticationConfig { Type = AuthenticationType.ApiKey, ApiKey = "key1" }
            }
        };
        var urls = new List<string> { "https://public.example.com/webhooks" };

        var settings = CreateSettingsWithEnabledSubmissionCompleted(endpoints: endpoints, urls: urls);
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("submission_completed");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(2).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventButNoEndpoints_DoesNotEnqueueTasks()
    {
        // Arrange
        var settings = CreateSettingsWithEnabledFormDeleted(endpoints: new List<WebHookEndpoint>(), urls: new List<string>());
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_deleted");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.DidNotReceive().EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventAndNullEndpoints_DoesNotEnqueueTasks()
    {
        // Arrange
        var settings = CreateSettingsWithEnabledFormEnabledStateChanged(endpoints: null, urls: null);
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_enabled_state_changed");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.DidNotReceive().EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEndpointAuthentication_PassesAuthenticationToTaskInstructions()
    {
        // Arrange
        var auth = new AuthenticationConfig
        {
            Type = AuthenticationType.ApiKey,
            ApiKey = "test-key",
            ApiKeyHeader = "X-API-KEY"
        };

        var endpoints = new List<WebHookEndpoint>
        {
            new() { 
                Url = "https://api.example.com/webhooks",
                Authentication = auth
            }
        };

        var settings = CreateSettingsWithEnabledFormCreated(endpoints: endpoints);
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_created");

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithCancellationToken_PassesTokenCorrectly()
    {
        // Arrange
        var settings = CreateSettingsWithEnabledFormCreated(endpoints: new List<WebHookEndpoint>
        {
            new() { Url = "https://api.example.com/webhooks" }
        });
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage("form_created");
        var cancellationToken = new CancellationToken();

        // Act
        await service.EnqueueWebHookAsync(message, cancellationToken);

        // Assert
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Theory]
    [InlineData("form_created")]
    [InlineData("form_updated")]
    [InlineData("form_enabled_state_changed")]
    [InlineData("submission_completed")]
    [InlineData("form_deleted")]
    public async Task EnqueueWebHookAsync_WithKnownEventNames_HandlesCorrectly(string eventName)
    {
        // Arrange
        var settings = CreateSettingsWithEnabledEvent(eventName, endpoints: new List<WebHookEndpoint>
        {
            new() { Url = "https://api.example.com/webhooks" }
        });
        _options.Value.Returns(settings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _options, _webHookServer);
        var message = CreateTestWebHookMessage(eventName);

        // Act
        await service.EnqueueWebHookAsync(message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    private static WebHookMessage<TestPayload> CreateTestWebHookMessage(string eventName)
    {
        var operation = eventName switch
        {
            "form_created" => WebHookOperation.FormCreated,
            "form_updated" => WebHookOperation.FormUpdated,
            "form_enabled_state_changed" => WebHookOperation.FormEnabledStateChanged,
            "submission_completed" => WebHookOperation.SubmissionCompleted,
            "form_deleted" => WebHookOperation.FormDeleted,
            _ => WebHookOperation.FormCreated // Use a valid operation for unknown events in tests
        };

        var payload = new TestPayload { Name = "Test" };
        return new WebHookMessage<TestPayload>(123, operation, payload);
    }

    private static WebHookSettings CreateSettingsWithDisabledEvents()
    {
        return new WebHookSettings
        {
            Events = new WebHookSettings.WebHookEvents
            {
                FormCreated = new WebHookSettings.EventSetting { EventName = "form_created", IsEnabled = false },
                FormUpdated = new WebHookSettings.EventSetting { EventName = "form_updated", IsEnabled = false },
                FormEnabledStateChanged = new WebHookSettings.EventSetting { EventName = "form_enabled_state_changed", IsEnabled = false },
                SubmissionCompleted = new WebHookSettings.EventSetting { EventName = "submission_completed", IsEnabled = false },
                FormDeleted = new WebHookSettings.EventSetting { EventName = "form_deleted", IsEnabled = false }
            }
        };
    }

    private static WebHookSettings CreateSettingsWithEnabledFormCreated(
        IEnumerable<WebHookEndpoint>? endpoints = null, 
        IEnumerable<string>? urls = null)
    {
        return new WebHookSettings
        {
            Events = new WebHookSettings.WebHookEvents
            {
                FormCreated = new WebHookSettings.EventSetting 
                { 
                    EventName = "form_created", 
                    IsEnabled = true,
                    WebHookEndpoints = endpoints,
                    WebHookUrls = urls
                },
                FormUpdated = new WebHookSettings.EventSetting { EventName = "form_updated", IsEnabled = false },
                FormEnabledStateChanged = new WebHookSettings.EventSetting { EventName = "form_enabled_state_changed", IsEnabled = false },
                SubmissionCompleted = new WebHookSettings.EventSetting { EventName = "submission_completed", IsEnabled = false },
                FormDeleted = new WebHookSettings.EventSetting { EventName = "form_deleted", IsEnabled = false }
            }
        };
    }

    private static WebHookSettings CreateSettingsWithEnabledFormUpdated(
        IEnumerable<WebHookEndpoint>? endpoints = null, 
        IEnumerable<string>? urls = null)
    {
        return new WebHookSettings
        {
            Events = new WebHookSettings.WebHookEvents
            {
                FormCreated = new WebHookSettings.EventSetting { EventName = "form_created", IsEnabled = false },
                FormUpdated = new WebHookSettings.EventSetting 
                { 
                    EventName = "form_updated", 
                    IsEnabled = true,
                    WebHookEndpoints = endpoints,
                    WebHookUrls = urls
                },
                FormEnabledStateChanged = new WebHookSettings.EventSetting { EventName = "form_enabled_state_changed", IsEnabled = false },
                SubmissionCompleted = new WebHookSettings.EventSetting { EventName = "submission_completed", IsEnabled = false },
                FormDeleted = new WebHookSettings.EventSetting { EventName = "form_deleted", IsEnabled = false }
            }
        };
    }

    private static WebHookSettings CreateSettingsWithEnabledSubmissionCompleted(
        IEnumerable<WebHookEndpoint>? endpoints = null, 
        IEnumerable<string>? urls = null)
    {
        return new WebHookSettings
        {
            Events = new WebHookSettings.WebHookEvents
            {
                FormCreated = new WebHookSettings.EventSetting { EventName = "form_created", IsEnabled = false },
                FormUpdated = new WebHookSettings.EventSetting { EventName = "form_updated", IsEnabled = false },
                FormEnabledStateChanged = new WebHookSettings.EventSetting { EventName = "form_enabled_state_changed", IsEnabled = false },
                SubmissionCompleted = new WebHookSettings.EventSetting 
                { 
                    EventName = "submission_completed", 
                    IsEnabled = true,
                    WebHookEndpoints = endpoints,
                    WebHookUrls = urls
                },
                FormDeleted = new WebHookSettings.EventSetting { EventName = "form_deleted", IsEnabled = false }
            }
        };
    }

    private static WebHookSettings CreateSettingsWithEnabledFormDeleted(
        IEnumerable<WebHookEndpoint>? endpoints = null, 
        IEnumerable<string>? urls = null)
    {
        return new WebHookSettings
        {
            Events = new WebHookSettings.WebHookEvents
            {
                FormCreated = new WebHookSettings.EventSetting { EventName = "form_created", IsEnabled = false },
                FormUpdated = new WebHookSettings.EventSetting { EventName = "form_updated", IsEnabled = false },
                FormEnabledStateChanged = new WebHookSettings.EventSetting { EventName = "form_enabled_state_changed", IsEnabled = false },
                SubmissionCompleted = new WebHookSettings.EventSetting { EventName = "submission_completed", IsEnabled = false },
                FormDeleted = new WebHookSettings.EventSetting 
                { 
                    EventName = "form_deleted", 
                    IsEnabled = true,
                    WebHookEndpoints = endpoints,
                    WebHookUrls = urls
                }
            }
        };
    }

    private static WebHookSettings CreateSettingsWithEnabledFormEnabledStateChanged(
        IEnumerable<WebHookEndpoint>? endpoints = null, 
        IEnumerable<string>? urls = null)
    {
        return new WebHookSettings
        {
            Events = new WebHookSettings.WebHookEvents
            {
                FormCreated = new WebHookSettings.EventSetting { EventName = "form_created", IsEnabled = false },
                FormUpdated = new WebHookSettings.EventSetting { EventName = "form_updated", IsEnabled = false },
                FormEnabledStateChanged = new WebHookSettings.EventSetting 
                { 
                    EventName = "form_enabled_state_changed", 
                    IsEnabled = true,
                    WebHookEndpoints = endpoints,
                    WebHookUrls = urls
                },
                SubmissionCompleted = new WebHookSettings.EventSetting { EventName = "submission_completed", IsEnabled = false },
                FormDeleted = new WebHookSettings.EventSetting { EventName = "form_deleted", IsEnabled = false }
            }
        };
    }

    private static WebHookSettings CreateSettingsWithEnabledEvent(
        string eventName,
        IEnumerable<WebHookEndpoint>? endpoints = null, 
        IEnumerable<string>? urls = null)
    {
        return eventName switch
        {
            "form_created" => CreateSettingsWithEnabledFormCreated(endpoints, urls),
            "form_updated" => CreateSettingsWithEnabledFormUpdated(endpoints, urls),
            "form_enabled_state_changed" => CreateSettingsWithEnabledFormEnabledStateChanged(endpoints, urls),
            "submission_completed" => CreateSettingsWithEnabledSubmissionCompleted(endpoints, urls),
            "form_deleted" => CreateSettingsWithEnabledFormDeleted(endpoints, urls),
            _ => throw new ArgumentException($"Unknown event name: {eventName}")
        };
    }

    private class TestPayload
    {
        public string Name { get; set; } = string.Empty;
    }
} 
