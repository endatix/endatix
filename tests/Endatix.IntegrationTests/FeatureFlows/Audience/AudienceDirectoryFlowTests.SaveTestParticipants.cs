using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// Saving the designer's edited presets as Test participants (endatix#1348): matched by identifier, new ones added,
/// changed ones updated, real ones refused, absent ones untouched, and a replay writes nothing.
/// </summary>
public sealed partial class AudienceDirectoryFlowTests
{
    [Fact]
    public async Task Saving_presets_adds_a_new_test_participant_updates_a_changed_one_and_returns_the_variables()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var seats = await admin.CreatePropertyAsync(formId, new { name = "Seats", dataType = "number" });
        await AddPersonWithValuesAsync(
            admin, formId, new("maria@example.com", IsTest: true, new() { [city] = "Sofia", [seats] = "3" }));
        await AddPersonWithValuesAsync(
            admin, formId, new("absent@example.com", IsTest: true, new() { [city] = "Ruse" }));

        // Act
        var saved = await SaveTestParticipantsAsync(
            admin,
            formId,
            """
            [{"name":"Maria@example.com","variables":{"city":"Varna"}},
             {"name":"new@example.com","variables":{"city":"Burgas","seats":12.5,"unknown":true}}]
            """);

        // Assert: Maria's seats is cleared, the absent one is untouched
        AssertJson(
            """
            [{"name":"absent@example.com","variables":{"city":"Ruse"}},
             {"name":"maria@example.com","variables":{"city":"Varna"}},
             {"name":"new@example.com","variables":{"city":"Burgas","seats":12.5}}]
            """,
            saved.GetProperty("variablePresets").GetProperty("presets"));
        AssertJson("""{"allowAdd":true,"allowEdit":true,"allowDelete":false}""", saved.GetProperty("editing"));
        AssertJson("""{"test":3,"real":0}""", saved.GetProperty("coverage").GetProperty("participants"));
        Assert.True(
            (await ListPeopleAsync(admin, formId, "?test=test")).GetProperty("items").EnumerateArray()
                .Single(person => person.GetProperty("identifier").GetString() == "new@example.com")
                .GetProperty("isTest").GetBoolean());
    }

    /// <summary>Price and Tags are stored as written, not in the form a save would write them, so a rewrite shows.</summary>
    [Fact]
    public async Task Saving_the_presets_the_variables_read_returned_twice_changes_nothing_for_every_data_type()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var values = await OnePropertyOfEveryDataTypeAsync(admin, formId);
        values[await admin.CreatePropertyAsync(formId, new { name = "Price", dataType = "number" })] = "1.50";
        values[await admin.CreatePropertyAsync(
            formId, new { name = "Tags", dataType = "multiple_choice", choicesJson = """["a","b"]""" })] =
            """["a", "b"]""";
        await AddPersonWithValuesAsync(admin, formId, new("tester@example.com", IsTest: true, values));
        var read = await GetVariablesAsync(admin, formId);
        var presets = read.GetProperty("variablePresets").GetProperty("presets").GetRawText();
        var before = await StoredRowsAsync(world.Services, formId);

        // Act
        var first = await SaveTestParticipantsAsync(admin, formId, presets);
        var second = await SaveTestParticipantsAsync(admin, formId, presets);

        // Assert
        Assert.Equal(before, await StoredRowsAsync(world.Services, formId));
        AssertJson(read.GetRawText(), first);
        AssertJson(read.GetRawText(), second);
    }

    [Fact]
    public async Task Saving_a_new_test_participant_twice_adds_them_once()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await FormWithCityAsync(admin);
        const string presets = """[{"name":"new@example.com","variables":{"city":"Sofia"}}]""";
        await SaveTestParticipantsAsync(admin, formId, presets);
        var before = await StoredRowsAsync(world.Services, formId);

        // Act
        var again = await SaveTestParticipantsAsync(admin, formId, presets);

        // Assert
        Assert.Equal(before, await StoredRowsAsync(world.Services, formId));
        Assert.Equal(1, again.GetProperty("variablePresets").GetProperty("totalPresets").GetInt32());
    }

    [Fact]
    public async Task Saving_a_preset_named_for_a_real_participant_is_refused_and_writes_nothing()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await FormWithCityAsync(admin);
        await AddParticipantAsync(admin, formId, "real@example.com");
        var before = await StoredRowsAsync(world.Services, formId);

        // Act
        using var response = await SaveTestParticipantsResponseAsync(
            admin,
            formId,
            """
            [{"name":"new@example.com","variables":{"city":"Sofia"}},
             {"name":"real@example.com","variables":{"city":"Varna"}}]
            """);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Real participants are read-only.", await response.Content.ReadAsStringAsync(admin.CancellationToken));
        Assert.Equal(before, await StoredRowsAsync(world.Services, formId));
    }

    [Theory]
    [InlineData("""[{"name":"Maria","variables":{}}]""")]
    [InlineData("""[{"name":"maria@example.com","variables":{"city":["not","text"]}},{"name":"x@example.com","variables":{}}]""")]
    [InlineData("""[{"name":"maria@example.com","variables":{}},{"name":"MARIA@example.com","variables":{}}]""")]
    [InlineData("""[{"name":"","variables":{}}]""")]
    [InlineData("""[{"name":"maria@example.com"}]""")]
    public async Task Saving_presets_the_audience_refuses_is_a_bad_request_and_writes_nothing(string presets)
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "single_choice", choicesJson = """["a"]""" });
        var before = await StoredRowsAsync(world.Services, formId);

        // Act
        using var response = await SaveTestParticipantsResponseAsync(admin, formId, presets);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await StoredRowsAsync(world.Services, formId));
    }

    [Fact]
    public async Task Saving_presets_on_another_tenants_form_is_not_found()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword },
            TestContext.Current.CancellationToken);
        var owner = await AsAsync(world, TestPersona.TenantAdmin);
        var stranger = new AudienceApi(
            await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: owner.CancellationToken),
            owner.CancellationToken);
        var formId = await FormWithCityAsync(owner);

        // Act
        using var response = await SaveTestParticipantsResponseAsync(
            stranger, formId, """[{"name":"new@example.com","variables":{}}]""");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Saving_presets_without_forms_edit_is_forbidden()
    {
        // Arrange
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword, AfterSeed = SeedViewerAsync },
            TestContext.Current.CancellationToken);
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var viewer = new AudienceApi(
            await world.AsAsync(
                TestPersona.CustomRole(ViewerRole), userName: ViewerUserName, cancellationToken: admin.CancellationToken),
            admin.CancellationToken);
        var formId = await FormWithCityAsync(admin);

        // Act
        using var response = await SaveTestParticipantsResponseAsync(
            viewer, formId, """[{"name":"new@example.com","variables":{}}]""");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>The other values are not rewritten: a number keeps the digits it was stored with.</summary>
    [Fact]
    public async Task Saving_a_changed_value_leaves_the_participants_other_values_as_stored()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var account = await admin.CreatePropertyAsync(formId, new { name = "Account", dataType = "number" });
        var price = await admin.CreatePropertyAsync(formId, new { name = "Price", dataType = "number" });
        await AddPersonWithValuesAsync(
            admin,
            formId,
            new("maria@example.com", IsTest: true, new() { [city] = "Sofia", [account] = "12345678901234567890", [price] = "1.50" }));
        var preset = (await GetVariablesAsync(admin, formId))
            .GetProperty("variablePresets").GetProperty("presets")[0];
        var variables = JsonNode.Parse(preset.GetProperty("variables").GetRawText())!.AsObject();
        variables["city"] = "Varna";

        // Act
        await SaveTestParticipantsAsync(
            admin,
            formId,
            new JsonArray(new JsonObject { ["name"] = "maria@example.com", ["variables"] = variables }).ToJsonString());

        // Assert
        Assert.Equal(
            new Dictionary<long, string> { [city] = "Varna", [account] = "12345678901234567890", [price] = "1.50" },
            await LiveCellsAsync(world.Services, formId));
    }

    /// <summary>A value stored before its Property's rule refused it (here written past the API) stays savable around.</summary>
    [Fact]
    public async Task Saving_a_changed_value_keeps_an_untouched_value_its_property_now_refuses()
    {
        // Arrange
        var world = await WorldAsync();
        var admin = await AsAsync(world, TestPersona.TenantAdmin);
        var formId = await admin.CreateFormAsync();
        var city = await admin.CreatePropertyAsync(formId, new { name = "City", dataType = "text" });
        var seats = await admin.CreatePropertyAsync(formId, new { name = "Seats", dataType = "number" });
        await AddPersonWithValuesAsync(
            admin, formId, new("maria@example.com", IsTest: true, new() { [city] = "Sofia", [seats] = "3" }));
        await OverwriteCellAsync(world.Services, seats, "many");

        // Act
        using var response = await SaveTestParticipantsResponseAsync(
            admin, formId, """[{"name":"maria@example.com","variables":{"city":"Varna","seats":"many"}}]""");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new Dictionary<long, string> { [city] = "Varna", [seats] = "many" },
            await LiveCellsAsync(world.Services, formId));
    }

    [Fact]
    public async Task Saving_a_new_test_participant_after_the_first_twenty_returns_their_preset()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);
        for (var index = 0; index < 20; index++)
        {
            using var added = await admin.PostAsync(
                $"/api/forms/{formId}/audience/people", new { identifier = $"t{index:00}@example.com", isTest = true });
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        // Act
        var saved = await SaveTestParticipantsAsync(
            admin, formId, """[{"name":"new@example.com","variables":{"city":"Sofia"}}]""");

        // Assert
        var presets = saved.GetProperty("variablePresets");
        Assert.Equal(21, presets.GetProperty("totalPresets").GetInt32());
        Assert.Contains(
            presets.GetProperty("presets").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "new@example.com");
    }

    [Fact]
    public async Task Saving_a_preset_that_names_a_variable_twice_is_a_bad_request()
    {
        // Arrange
        var admin = await AdminAsync();
        var formId = await FormWithCityAsync(admin);

        // Act
        using var response = await admin.Client.PutAsync(
            $"/api/forms/{formId}/audience/variables/test-participants",
            new StringContent(
                """{"presets":[{"name":"new@example.com","variables":{"city":"Sofia","city":"Varna"}}]}""",
                System.Text.Encoding.UTF8,
                "application/json"),
            admin.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The form's live cells by property id (a form with one participant).</summary>
    private static async Task<Dictionary<long, string>> LiveCellsAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var memberships = await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.FormId == formId)
            .Select(membership => membership.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        return await db.PropertyValues.IgnoreQueryFilters()
            .Where(cell => memberships.Contains(cell.MembershipId) && !cell.IsDeleted)
            .ToDictionaryAsync(cell => cell.PropertyId, cell => cell.Value, TestContext.Current.CancellationToken);
    }

    /// <summary>Writes a cell past the API's value rules, as data stored before a rule changed would be.</summary>
    private static async Task OverwriteCellAsync(IServiceProvider services, long propertyId, string value)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var cell = await db.PropertyValues.IgnoreQueryFilters()
            .SingleAsync(row => row.PropertyId == propertyId && !row.IsDeleted, TestContext.Current.CancellationToken);
        cell.SetValue(value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> SaveTestParticipantsResponseAsync(
        AudienceApi api, long formId, string presetsJson) =>
        api.Client.PutAsJsonAsync(
            $"/api/forms/{formId}/audience/variables/test-participants",
            new JsonObject { ["presets"] = JsonNode.Parse(presetsJson) },
            api.CancellationToken);

    private static async Task<JsonElement> SaveTestParticipantsAsync(AudienceApi api, long formId, string presetsJson)
    {
        using var response = await SaveTestParticipantsResponseAsync(api, formId, presetsJson);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await api.ReadAsync(response);
    }

    /// <summary>
    /// Every membership and cell row of the form, removed ones included, with when each last changed: equal before and
    /// after means nothing was written.
    /// </summary>
    private static async Task<string> StoredRowsAsync(IServiceProvider services, long formId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAudienceDbContext>();
        var memberships = await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.FormId == formId)
            .OrderBy(membership => membership.Id)
            .Select(membership => new { membership.Id, membership.IsTest, membership.IsDeleted, membership.ModifiedAt })
            .ToListAsync(TestContext.Current.CancellationToken);
        var ids = memberships.Select(membership => membership.Id).ToList();
        var cells = await db.PropertyValues.IgnoreQueryFilters()
            .Where(cell => ids.Contains(cell.MembershipId))
            .OrderBy(cell => cell.Id)
            .Select(cell => new { cell.Id, cell.Value, cell.IsDeleted, cell.ModifiedAt })
            .ToListAsync(TestContext.Current.CancellationToken);
        return JsonSerializer.Serialize(new { memberships, cells });
    }
}
