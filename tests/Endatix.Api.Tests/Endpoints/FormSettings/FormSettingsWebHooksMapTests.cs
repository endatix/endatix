using System.Text.Json;
using Endatix.Api.Endpoints.FormSettings;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormSettings;
using FluentAssertions;

namespace Endatix.Api.Tests.Endpoints.FormSettings;

/// <summary>The webhooks section on a settings read: null when the tenant's apply, and readable whatever was stored.</summary>
public sealed class FormSettingsWebHooksMapTests
{
    [Fact]
    public void Map_NoWebHooksOfItsOwn_IsNull()
    {
        var model = FormSettingsModel.Map(new FormSettingsDto(1, new PdfSettings(), WebHooks: null));

        model.WebHooks.Should().BeNull("the tenant's webhooks apply");
    }

    [Fact]
    public void Map_NoEvents_IsAnEmptySectionNotNull()
    {
        var model = FormSettingsModel.Map(new FormSettingsDto(1, new PdfSettings(), new WebHookConfiguration()));

        model.WebHooks!.Events.Should().BeEmpty("no events of its own still replaces the tenant's");
    }

    // The form endpoints accept these, and the copy keeps them as stored.
    [Theory]
    [InlineData("""{"Events":null}""")]
    [InlineData("""{"Events":{"FormCreated":null}}""")]
    [InlineData("""{"Events":{"FormCreated":{"IsEnabled":true,"WebHookEndpoints":null}}}""")]
    [InlineData("""{"Events":{"FormCreated":{"IsEnabled":true,"WebHookEndpoints":[null,{"Url":null}]}}}""")]
    public void Map_JsonNullsWhereObjectsBelong_ReadAsNothingConfiguredThere(string stored)
    {
        var webHooks = JsonSerializer.Deserialize<WebHookConfiguration>(stored)!;

        var map = () => FormSettingsModel.Map(new FormSettingsDto(1, new PdfSettings(), webHooks));

        map.Should().NotThrow();
    }
}
