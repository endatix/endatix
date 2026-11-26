using Endatix.Core.Abstractions.Repositories;
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
    private const string SECTION_URL = "https://section.example.test/hooks";

    private readonly List<Form> _forms = [];
    private readonly List<TenantSettings> _tenantSettings = [];
    private readonly IRepository<Form> _formRepository = Substitute.For<IRepository<Form>>();
    private readonly IRepository<TenantSettings> _tenantSettingsRepository = Substitute.For<IRepository<TenantSettings>>();
    private readonly Dictionary<long, FormSettings> _formSettings = [];
    private readonly IFormSettingsRepository _formSettingsRepository = Substitute.For<IFormSettingsRepository>();

    public WebHookEventConfigReaderTests()
    {
        // Evaluates the reader's specifications against the seeded rows, so the tenant scoping is exercised.
        _formRepository
            .FirstOrDefaultAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<FormSpecifications.ByIdForTenant>().Evaluate(_forms).FirstOrDefault());
        _tenantSettingsRepository
            .FirstOrDefaultAsync(Arg.Any<TenantSettingsByTenantIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<TenantSettingsByTenantIdSpec>().Evaluate(_tenantSettings).FirstOrDefault());
        _formSettingsRepository
            .ReadAsync(Arg.Any<Form>(), Arg.Any<CancellationToken>())
            .Returns(call => _formSettings.GetValueOrDefault(call.Arg<Form>().Id) ?? FormSettings.For(call.Arg<Form>()));
    }

    [Fact]
    public async Task GetEventConfigAsync_SectionSameAsTheColumnInAnotherCasing_UsesIt()
    {
        // Arrange — a settings write stores camelCase; the column holds the same settings in Pascal case.
        var form = SeedForm(TenantId);
        StoreSection(form, "{\"events\":{\"FormUpdated\":{\"isEnabled\":true,\"webHookEndpoints\":[{\"url\":\"" + FormUrl + "\"}]}}}");
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(FormUrl);
    }

    [Fact]
    public async Task GetEventConfigAsync_ColumnWrittenAloneAfterTheCopy_UsesTheColumn()
    {
        // Arrange — an older instance changed the form's webhooks after the migration copied them: only the column moved.
        var form = SeedForm(TenantId);
        StoreSection(form, StoredEvents($"\"FormUpdated\":{Endpoint(SECTION_URL)}"));
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(FormUrl);
    }

    [Fact]
    public async Task GetEventConfigAsync_ColumnClearedAloneAfterTheCopy_UsesTheTenant()
    {
        // Arrange — an older instance cleared the form's webhooks; the section still holds the copy.
        var form = SeedForm(TenantId);
        StoreSection(form, StoredEvents($"\"FormUpdated\":{Endpoint(SECTION_URL)}"));
        form.UpdateWebHookSettings(null);
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(TenantUrl);
    }

    [Theory]
    [InlineData("formupdated")]
    [InlineData("FORMUPDATED")]
    public async Task GetEventConfigAsync_StoredKeyInAnyCase_ReadsIt(string storedKey)
    {
        // Arrange — the copy keeps the stored document as it is, and stored keys match event names in any case.
        var stored = "{\"events\":{\"" + storedKey + "\":" + Endpoint(SECTION_URL) + "}}";
        var form = SeedForm(TenantId);
        StoreColumn(form, stored);
        StoreSection(form, stored);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(SECTION_URL);
    }

    [Fact]
    public async Task GetEventConfigAsync_FormWithoutASection_UsesItsColumn()
    {
        // Arrange — a form an older instance created during a rolling deploy has no section yet.
        SeedForm(TenantId);
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(FormUrl);
    }

    [Fact]
    public async Task GetEventConfigAsync_FormWithNoEventsOfItsOwn_DoesNotFallBackToTheTenant()
    {
        // Arrange — any stored form document replaces the tenant's, as the column always did.
        var form = SeedForm(TenantId);
        StoreColumn(form, """{"Events":{}}""");
        StoreSection(form, """{"events":{}}""");
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(Lookup(), CancellationToken.None);

        // Assert
        config.Should().BeNull();
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

    [Fact]
    public async Task GetEventConfigAsync_NoTenant_ThrowsWithoutReadingSettings()
    {
        // Arrange
        SeedTenantSettings(TenantId);
        var reader = CreateReader();

        // Act
        var read = () => reader.GetEventConfigAsync(new WebHookEventLookup(0, EventName, FormId), CancellationToken.None);

        // Assert — a missing tenant fails the delivery visibly instead of reading as "not configured".
        await read.Should().ThrowAsync<ArgumentException>();
        _formRepository.ReceivedCalls().Should().BeEmpty();
        _tenantSettingsRepository.ReceivedCalls().Should().BeEmpty();
    }

    private static WebHookEventLookup Lookup() => new(TenantId, EventName, FormId);

    private WebHookEventConfigReader CreateReader() =>
        new(new FormWebHookSettingsReader(_formRepository, _formSettingsRepository), _tenantSettingsRepository, NullLogger<WebHookEventConfigReader>.Instance);

    [Theory]
    [InlineData("SubmissionCollectionStatusChanged")]
    [InlineData("submissioncollectionstatuschanged")]
    public async Task GetEventConfigAsync_SubmissionClosedStoredUnderTheRetiredKey_ReadsIt(string storedKey)
    {
        // Arrange — an earlier instance or an older Hub saved the retired key after the migration ran.
        StoreRawTenantSettings(TenantId, StoredEvents($"\"{storedKey}\":{Endpoint(TenantUrl)}"));
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(
            new WebHookEventLookup(TenantId, WebHookOperation.SubmissionClosed.EventName, FormId: null), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(TenantUrl);
    }

    [Fact]
    public async Task GetEventConfigAsync_BothKeysStored_ReadsTheNewOne()
    {
        // Arrange
        StoreRawTenantSettings(TenantId, StoredEvents($"\"SubmissionCollectionStatusChanged\":{Endpoint("https://old.example.test")},\"SubmissionClosed\":{Endpoint(TenantUrl)}"));
        var reader = CreateReader();

        // Act
        var config = await reader.GetEventConfigAsync(
            new WebHookEventLookup(TenantId, WebHookOperation.SubmissionClosed.EventName, FormId: null), CancellationToken.None);

        // Assert
        config!.WebHookEndpoints.Should().ContainSingle().Which.Url.Should().Be(TenantUrl);
    }

    private static string StoredEvents(string events) => "{\"Events\":{" + events + "}}";

    private static string Endpoint(string url) => "{\"IsEnabled\":true,\"WebHookEndpoints\":[{\"Url\":\"" + url + "\"}]}";

    // As a row stored by an earlier writer loads: the constructor's retired-key guard is not on the read path.
    private void StoreRawTenantSettings(long tenantId, string json)
    {
        var settings = new TenantSettings(tenantId, submissionTokenExpiryHours: null, isSubmissionTokenValidAfterCompletion: false);
        typeof(TenantSettings).GetProperty(nameof(TenantSettings.WebHookSettingsJson))!.SetValue(settings, json);
        _tenantSettings.Add(settings);
    }

    private Form SeedForm(long tenantId)
    {
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Test Form"));
        form.Id = FormId;
        form.UpdateWebHookSettings(ConfigFor(FormUrl));
        _forms.Add(form);
        return form;
    }

    // As an earlier writer stored it: the entity's guards are not on the read path.
    private static void StoreColumn(Form form, string json) =>
        typeof(Form).GetProperty(nameof(Form.WebHookSettingsJson))!.SetValue(form, json);

    // As the migration copies a document: as it is, whatever its casing.
    private void StoreSection(Form form, string json)
    {
        var settings = FormSettings.For(form);
        typeof(FormSettings).GetProperty(nameof(FormSettings.WebHooksJson))!.SetValue(settings, json);
        _formSettings[form.Id] = settings;
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
