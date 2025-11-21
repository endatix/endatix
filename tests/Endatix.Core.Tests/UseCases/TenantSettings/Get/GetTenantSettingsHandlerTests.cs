using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.TenantSettings.Get;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;
using SlackSettingsEntity = Endatix.Core.Entities.SlackSettings;
using WebHookConfigurationEntity = Endatix.Core.Entities.WebHookConfiguration;
using WebHookEventConfig = Endatix.Core.Entities.WebHookEventConfig;
using WebHookEndpointConfig = Endatix.Core.Entities.WebHookEndpointConfig;
using WebHookAuthConfig = Endatix.Core.Entities.WebHookAuthConfig;
using CustomExportConfigurationEntity = Endatix.Core.Entities.CustomExportConfiguration;

namespace Endatix.Core.Tests.UseCases.TenantSettings.Get;

public class GetTenantSettingsHandlerTests
{
    private readonly IRepository<TenantSettingsEntity> _repository;
    private readonly ITenantContext _tenantContext;
    private readonly GetTenantSettingsHandler _handler;

    public GetTenantSettingsHandlerTests()
    {
        _repository = Substitute.For<IRepository<TenantSettingsEntity>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _handler = new GetTenantSettingsHandler(_repository, _tenantContext);
    }

    [Fact]
    public async Task Handle_TenantSettingsNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        TenantSettingsEntity? notFoundSettings = null;
        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(notFoundSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Tenant settings not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsTenantSettingsDto()
    {
        // Arrange
        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.TenantId.Should().Be(SampleData.TENANT_ID);
        result.Value.SubmissionTokenExpiryHours.Should().Be(24);
        result.Value.IsSubmissionTokenValidAfterCompletion.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SlackSettingsWithToken_MasksTokenInDto()
    {
        // Arrange
        var slackSettings = new SlackSettingsEntity
        {
            Token = "xoxb-secret-slack-token",
            EndatixHubBaseUrl = "https://hub.endatix.com",
            ChannelId = "C01234567",
            Active = true
        };

        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateSlackSettings(slackSettings);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.SlackSettings.Should().NotBeNull();
        result.Value.SlackSettings!.Token.Should().NotBeNull();
        result.Value.SlackSettings.Token.Should().MatchRegex("^\\*+$"); // Token should be masked with asterisks
        result.Value.SlackSettings.EndatixHubBaseUrl.Should().Be("https://hub.endatix.com");
        result.Value.SlackSettings.ChannelId.Should().Be("C01234567");
        result.Value.SlackSettings.Active.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WebHookSettingsWithUrl_ReturnsUrlInDto()
    {
        // Arrange
        var webHookConfig = new WebHookConfigurationEntity
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
                            Url = "https://api.example.com/webhooks/submissions",
                            Authentication = new WebHookAuthConfig
                            {
                                Type = "ApiKey",
                                ApiKey = "secret-api-key-12345",
                                ApiKeyHeader = "X-API-Key"
                            }
                        }
                    }
                }
            }
        };

        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateWebHookSettings(webHookConfig);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.WebHookSettings.Should().NotBeNull();
        result.Value.WebHookSettings!.Events.Should().ContainKey("SubmissionCompleted");

        var eventConfig = result.Value.WebHookSettings.Events["SubmissionCompleted"];
        eventConfig.IsEnabled.Should().BeTrue();
        eventConfig.WebHookEndpoints.Should().HaveCount(1);

        var endpoint = eventConfig.WebHookEndpoints[0];
        endpoint.Url.Should().Be("https://api.example.com/webhooks/submissions");
    }

    [Fact]
    public async Task Handle_InactiveSlackSettings_ReturnsSlackSettingsWithActiveFalse()
    {
        // Arrange
        var slackSettings = new SlackSettingsEntity
        {
            Token = "xoxb-secret-slack-token",
            EndatixHubBaseUrl = "https://hub.endatix.com",
            ChannelId = "C01234567",
            Active = false // Inactive
        };

        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateSlackSettings(slackSettings);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.SlackSettings.Should().NotBeNull();
        result.Value.SlackSettings!.Active.Should().BeFalse();
        result.Value.SlackSettings.Token.Should().NotBeNull();
        result.Value.SlackSettings.Token.Should().MatchRegex("^\\*+$"); // Token should still be masked
        result.Value.SlackSettings.EndatixHubBaseUrl.Should().Be("https://hub.endatix.com");
        result.Value.SlackSettings.ChannelId.Should().Be("C01234567");
    }

    [Fact]
    public async Task Handle_CustomExportsWithValidData_ReturnsCustomExportsInDto()
    {
        // Arrange
        var customExports = new List<CustomExportConfigurationEntity>
        {
            new CustomExportConfigurationEntity
            {
                Id = 1,
                Name = "Nested Loops Export",
                SqlFunctionName = "export_form_submissions_nested_loops"
            },
            new CustomExportConfigurationEntity
            {
                Id = 2,
                Name = "Standard Export",
                SqlFunctionName = "export_form_submissions_standard"
            }
        };

        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateCustomExports(customExports);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.CustomExports.Should().NotBeNull();
        result.Value.CustomExports.Should().HaveCount(2);

        result.Value.CustomExports![0].Id.Should().Be(1);
        result.Value.CustomExports[0].Name.Should().Be("Nested Loops Export");
        result.Value.CustomExports[0].SqlFunctionName.Should().Be("export_form_submissions_nested_loops");

        result.Value.CustomExports[1].Id.Should().Be(2);
        result.Value.CustomExports[1].Name.Should().Be("Standard Export");
        result.Value.CustomExports[1].SqlFunctionName.Should().Be("export_form_submissions_standard");
    }

    [Fact]
    public async Task Handle_NoCustomExports_ReturnsNullCustomExports()
    {
        // Arrange
        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.CustomExports.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyCustomExportsList_ReturnsNullCustomExports()
    {
        // Arrange
        var customExports = new List<CustomExportConfigurationEntity>();

        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateCustomExports(customExports);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.CustomExports.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SingleCustomExport_ReturnsSingleItemInDto()
    {
        // Arrange
        var customExports = new List<CustomExportConfigurationEntity>
        {
            new CustomExportConfigurationEntity
            {
                Id = 1,
                Name = "My Custom Export",
                SqlFunctionName = "custom_export_function"
            }
        };

        var tenantSettings = new TenantSettingsEntity(
            tenantId: SampleData.TENANT_ID,
            submissionTokenExpiryHours: 24,
            isSubmissionTokenValidAfterCompletion: false);
        tenantSettings.UpdateCustomExports(customExports);

        var request = new GetTenantSettingsQuery();
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _repository.FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(tenantSettings);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Value.CustomExports.Should().NotBeNull();
        result.Value.CustomExports.Should().HaveCount(1);
        result.Value.CustomExports![0].Id.Should().Be(1);
        result.Value.CustomExports[0].Name.Should().Be("My Custom Export");
        result.Value.CustomExports[0].SqlFunctionName.Should().Be("custom_export_function");
    }
}
