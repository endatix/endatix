using Endatix.Core.Entities;
using Endatix.Core.Features.WebHooks;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.WebHooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Infrastructure.Tests.Features.WebHooks;

public sealed class WebHookEventConfigReaderTests
{
    private const long TenantId = 1;
    private const long OtherTenantId = 2;
    private const long FormId = 100;
    private const string EventName = "form_updated";
    private const string FormUrl = "https://form.example.test/hooks";
    private const string TenantUrl = "https://tenant.example.test/hooks";

    private readonly List<Form> _forms = [];
    private readonly List<TenantSettings> _tenantSettings = [];
    private readonly IRepository<Form> _formRepository = Substitute.For<IRepository<Form>>();
    private readonly IRepository<TenantSettings> _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();

    public WebHookEventConfigReaderTests()
    {
        // Evaluates the reader's specifications against the seeded rows, so the tenant scoping is exercised.
        _formRepository
            .FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<FormSpecifications.ByIdForTenant>().Evaluate(_forms).FirstOrDefault());
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<TenantSettingsByTenantIdSpec>().Evaluate(_tenantSettings).FirstOrDefault());
    }

    [Fact]
    public async Task GetEventConfigAsync_FormOfSameTenant_UsesFormConfig()
    {
        // Arrange
        SeedForm(TenantId);
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(FormUrl);
    }

    [Fact]
    public async Task GetEventConfigAsync_FormOfOtherTenant_FallsBackToTenantConfig()
    {
        // Arrange — the form id names a form of another tenant, which configures endpoints of its own.
        SeedForm(OtherTenantId);
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(TenantUrl);
    }

    [Fact]
    public async Task GetEventConfigAsync_FormOfOtherTenantAndNoTenantConfig_ReturnsNull()
    {
        // Arrange
        SeedForm(OtherTenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert — the other tenant's endpoints are never lent to this tenant's event.
        config.Should().BeNull();
    }

    private static WebHookEventLookup Lookup() => new(TenantId, EventName, FormId);

    private WebHookEventConfigReader CreateReader() =>
        new(_formRepository, _tenantSettingsRepository, NullLogger<WebHookEventConfigReader>.Instance);

    private void SeedForm(long tenantId)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Test Form"));
        form.Id = FormId;
        form.UpdateWebHookSettings(ConfigFor(FormUrl));
        _forms.Add(form);
    }

    private void SeedTenantSettings(long tenantId)
    {
        var settings = new TenantSettings(tenantId, submissionTokenExpiryHours: null, isSubmissionTokenValidAfterCompletion: false);
        settings.UpdateWebHookSettings(ConfigFor(TenantUrl));
        _tenantSettings.Add(settings);
    }

    private static WebHookConfiguration ConfigFor(string url) => new()
    {
        Events = new Dictionary<string, WebHookEventConfig>
        {
            ["FormUpdated"] = new()
            {
                IsEnabled = true,
                WebHookEndpoints = [new WebHookEndpointConfig { Url = url }],
            },
        },
    };
}
