using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.WebHooks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public class BackgroundTaskWebHookServiceTests
{
    private readonly ILogger<BackgroundTaskWebHookService> _logger;
    private readonly IBackgroundTasksQueue _backgroundQueue;
    private readonly WebHookServer _webHookServer;
    private readonly IRepository<Form> _formRepository;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;
    private const long TEST_TENANT_ID = 1;
    private const long TEST_FORM_ID = 100;

    public BackgroundTaskWebHookServiceTests()
    {
        _logger = Substitute.For<ILogger<BackgroundTaskWebHookService>>();
        _backgroundQueue = Substitute.For<IBackgroundTasksQueue>();

        // WebHookServer requires HttpClient and ILogger<WebHookServer> constructor arguments
        var httpClient = Substitute.For<HttpClient>();
        var webHookLogger = Substitute.For<ILogger<WebHookServer>>();
        _webHookServer = Substitute.For<WebHookServer>(httpClient, webHookLogger);

        _formRepository = Substitute.For<IRepository<Form>>();
        _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithDisabledEvent_DoesNotEnqueueTasks()
    {
        // Arrange
        var tenantSettings = CreateTenantSettingsWithDisabledEvent("FormCreated");
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormCreated);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.DidNotReceive().EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventAndEndpoints_EnqueuesTasksForEachEndpoint()
    {
        // Arrange
        var tenantSettings = CreateTenantSettingsWithEnabledEvent("FormCreated", new List<string>
        {
            "https://api1.example.com/webhooks",
            "https://api2.example.com/webhooks"
        });
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormCreated);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(2).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithFormLevelConfig_UsesFormConfigNotTenantConfig()
    {
        // Arrange
        var form = CreateFormWithEnabledEvent("FormUpdated", new List<string>
        {
            "https://form-specific.example.com/webhooks"
        });
        var tenantSettings = CreateTenantSettingsWithEnabledEvent("FormUpdated", new List<string>
        {
            "https://tenant-level.example.com/webhooks"
        });

        _formRepository.GetByIdAsync(TEST_FORM_ID, Arg.Any<CancellationToken>())
            .Returns(form);
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormUpdated);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None, formId: TEST_FORM_ID);

        // Assert
        // Should enqueue only 1 task (from form config), not 2 (tenant config is ignored)
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEnabledEventButNoEndpoints_DoesNotEnqueueTasks()
    {
        // Arrange
        var tenantSettings = CreateTenantSettingsWithEnabledEvent("FormDeleted", new List<string>());
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormDeleted);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.DidNotReceive().EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithNoConfiguration_DoesNotEnqueueTasks()
    {
        // Arrange
        TenantSettings? nullTenantSettings = null;
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(nullTenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormEnabledStateChanged);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.DidNotReceive().EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithEndpointAuthentication_EnqueuesTask()
    {
        // Arrange
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["FormCreated"] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = new List<WebHookEndpointConfig>
                    {
                        new WebHookEndpointConfig
                        {
                            Url = "https://secure.example.com/webhooks",
                            Authentication = new WebHookAuthConfig
                            {
                                Type = "ApiKey",
                                ApiKey = "test-key",
                                ApiKeyHeader = "X-API-KEY"
                            }
                        }
                    }
                }
            }
        };

        var tenantSettings = new TenantSettings(TEST_TENANT_ID, submissionTokenExpiryHours: null, isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateWebHookSettings(webHookConfig);

        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormCreated);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithCancellationToken_PassesTokenCorrectly()
    {
        // Arrange
        var tenantSettings = CreateTenantSettingsWithEnabledEvent("FormCreated", new List<string>
        {
            "https://api.example.com/webhooks"
        });
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.FormCreated);
        var cancellationToken = new CancellationToken();

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, cancellationToken);

        // Assert
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Theory]
    [InlineData("form_created", "FormCreated")]
    [InlineData("form_updated", "FormUpdated")]
    [InlineData("form_enabled_state_changed", "FormEnabledStateChanged")]
    [InlineData("submission_completed", "SubmissionCompleted")]
    [InlineData("form_deleted", "FormDeleted")]
    public async Task EnqueueWebHookAsync_WithSnakeCaseEventNames_ConvertsCorrectlyToPascalCase(string snakeCaseEvent, string pascalCaseEvent)
    {
        // Arrange
        var tenantSettings = CreateTenantSettingsWithEnabledEvent(pascalCaseEvent, new List<string>
        {
            "https://api.example.com/webhooks"
        });
        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);

        var operation = snakeCaseEvent switch
        {
            "form_created" => WebHookOperation.FormCreated,
            "form_updated" => WebHookOperation.FormUpdated,
            "form_enabled_state_changed" => WebHookOperation.FormEnabledStateChanged,
            "submission_completed" => WebHookOperation.SubmissionCompleted,
            "form_deleted" => WebHookOperation.FormDeleted,
            _ => WebHookOperation.FormCreated
        };

        var message = CreateTestWebHookMessage(operation);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(1).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    [Fact]
    public async Task EnqueueWebHookAsync_WithMultipleEndpointsIncludingAuth_EnqueuesAllTasks()
    {
        // Arrange
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                ["SubmissionCompleted"] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = new List<WebHookEndpointConfig>
                    {
                        new WebHookEndpointConfig
                        {
                            Url = "https://secure.example.com/webhooks",
                            Authentication = new WebHookAuthConfig
                            {
                                Type = "ApiKey",
                                ApiKey = "key1"
                            }
                        },
                        new WebHookEndpointConfig
                        {
                            Url = "https://public.example.com/webhooks"
                        }
                    }
                }
            }
        };

        var tenantSettings = new TenantSettings(TEST_TENANT_ID, submissionTokenExpiryHours: null, isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateWebHookSettings(webHookConfig);

        _tenantSettingsRepository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(tenantSettings);

        var service = new BackgroundTaskWebHookService(_logger, _backgroundQueue, _formRepository, _tenantSettingsRepository, _webHookServer);
        var message = CreateTestWebHookMessage(WebHookOperation.SubmissionCompleted);

        // Act
        await service.EnqueueWebHookAsync(TEST_TENANT_ID, message, CancellationToken.None);

        // Assert
        await _backgroundQueue.Received(2).EnqueueAsync(Arg.Any<Func<CancellationToken, ValueTask>>());
    }

    private static WebHookMessage<TestPayload> CreateTestWebHookMessage(WebHookOperation operation)
    {
        var payload = new TestPayload { Name = "Test" };
        return new WebHookMessage<TestPayload>(123, operation, payload);
    }

    private static TenantSettings CreateTenantSettingsWithDisabledEvent(string eventName)
    {
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                [eventName] = new WebHookEventConfig
                {
                    IsEnabled = false,
                    WebHookEndpoints = new List<WebHookEndpointConfig>()
                }
            }
        };

        var tenantSettings = new TenantSettings(TEST_TENANT_ID, submissionTokenExpiryHours: null, isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateWebHookSettings(webHookConfig);
        return tenantSettings;
    }

    private static TenantSettings CreateTenantSettingsWithEnabledEvent(string eventName, List<string> urls)
    {
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                [eventName] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = urls.Select(url => new WebHookEndpointConfig { Url = url }).ToList()
                }
            }
        };

        var tenantSettings = new TenantSettings(TEST_TENANT_ID, submissionTokenExpiryHours: null, isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateWebHookSettings(webHookConfig);
        return tenantSettings;
    }

    private static Form CreateFormWithEnabledEvent(string eventName, List<string> urls)
    {
        var webHookConfig = new WebHookConfiguration
        {
            Events = new Dictionary<string, WebHookEventConfig>
            {
                [eventName] = new WebHookEventConfig
                {
                    IsEnabled = true,
                    WebHookEndpoints = urls.Select(url => new WebHookEndpointConfig { Url = url }).ToList()
                }
            }
        };

        var form = new Form(TEST_TENANT_ID, "Test Form")
        {
            Id = TEST_FORM_ID
        };
        form.UpdateWebHookSettings(webHookConfig);
        return form;
    }

    private class TestPayload
    {
        public string Name { get; set; } = string.Empty;
    }
}
