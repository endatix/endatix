using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.DataLists;

/// <summary>
/// The public data-list routes answer only for a list the form's definitions use: search follows the active
/// definition, display values follow any definition of the form. Any other list, of another form, of no form or
/// of another tenant, answers like a list that does not exist.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P0")]
public sealed class PublicDataListScopeFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string BOUND_VALUE = "bound-choice";
    private const string OTHER_FORM_VALUE = "other-form-secret-choice";
    private const string UNUSED_VALUE = "unused-secret-choice";
    private const string OTHER_TENANT_VALUE = "other-tenant-secret-choice";
    private const string DRAFT_VALUE = "draft-only-choice";
    private const string OLDER_VALUE = "older-only-choice";
    private const long MISSING_LIST_ID = 987_654_321_012;

    [Fact]
    public async Task A_form_token_reads_the_list_its_own_form_uses()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);

        using var response = await client.GetAsync(SearchUrl(setup.PublicFormId, setup.BoundListId), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain(BOUND_VALUE);
    }

    [Fact]
    public async Task A_form_token_resolves_display_values_of_the_list_its_own_form_uses()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);

        using var response = await client.GetAsync(DisplayValuesUrl(setup.PublicFormId, setup.BoundListId, BOUND_VALUE), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain("Bound label");
    }

    [Theory]
    [InlineData(nameof(Setup.OtherFormListId), OTHER_FORM_VALUE)]
    [InlineData(nameof(Setup.UnusedListId), UNUSED_VALUE)]
    [InlineData(nameof(Setup.OtherTenantListId), OTHER_TENANT_VALUE)]
    [InlineData(nameof(Setup.DraftOnlyListId), DRAFT_VALUE)]
    [InlineData(nameof(Setup.OlderOnlyListId), OLDER_VALUE)]
    public async Task A_form_token_cannot_search_a_list_its_active_definition_does_not_use(string list, string secret)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);

        using var response = await client.GetAsync(SearchUrl(setup.PublicFormId, setup.ListId(list)), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        body.Should().NotContain(secret);
        await ShouldLookLikeAMissingListAsync(client, response, SearchUrl(setup.PublicFormId, MISSING_LIST_ID), ct);
    }

    [Theory]
    [InlineData(nameof(Setup.OtherFormListId), OTHER_FORM_VALUE)]
    [InlineData(nameof(Setup.UnusedListId), UNUSED_VALUE)]
    [InlineData(nameof(Setup.OtherTenantListId), OTHER_TENANT_VALUE)]
    public async Task A_form_token_cannot_resolve_display_values_of_a_list_its_form_does_not_use(string list, string secret)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);

        using var response = await client.GetAsync(DisplayValuesUrl(setup.PublicFormId, setup.ListId(list), secret), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        body.Should().NotContain(secret);
        await ShouldLookLikeAMissingListAsync(
            client,
            response,
            DisplayValuesUrl(setup.PublicFormId, MISSING_LIST_ID, secret),
            ct);
    }

    [Theory]
    [InlineData(nameof(Setup.DraftOnlyListId), DRAFT_VALUE, "Draft label")]
    [InlineData(nameof(Setup.OlderOnlyListId), OLDER_VALUE, "Older label")]
    public async Task A_form_token_resolves_display_values_of_a_list_another_definition_of_its_form_uses(
        string list,
        string value,
        string label)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);

        using var response = await client.GetAsync(DisplayValuesUrl(setup.PublicFormId, setup.ListId(list), value), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain(label);
    }

    [Theory]
    [InlineData("nested-panel", true)]
    [InlineData("dynamic-panel", false)]
    [InlineData("matrix-column", true)]
    [InlineData("matrix-dynamic-column", false)]
    public async Task A_list_bound_anywhere_in_the_active_definition_is_readable(string placement, bool numericId)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        var tenantId = world.Tenants[0].Id;

        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var list = NewList(tenantId, placement, $"{placement}-choice", $"{placement} label");
        db.DataLists.Add(list);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: placement, IsEnabled: true, IsPublic: true));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);

        var id = numericId ? list.Id.ToString() : $"\"{list.Id}\"";
        form.AddFormDefinition(new FormDefinition(tenantId, isDraft: false, jsonData: Definition(Placement(placement, id))));
        await db.SaveChangesAsync(ct);

        using var client = await AnonymousWithFormTokenAsync(world, form.Id, ct);
        using var response = await client.GetAsync(SearchUrl(form.Id, list.Id), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain($"{placement}-choice");
    }

    [Fact]
    public async Task A_list_bound_by_a_definition_saved_after_the_token_was_issued_becomes_readable()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);
        using (var before = await client.GetAsync(SearchUrl(setup.PublicFormId, setup.UnusedListId), ct))
        {
            before.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using var staff = await setup.World.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var json = Definition(Question("pick", $"\"{setup.BoundListId}\"") + "," + Question("later", $"\"{setup.UnusedListId}\""));
        using (var saved = await staff.PatchAsJsonAsync($"/api/forms/{setup.PublicFormId}/definition", new { jsonData = json }, ct))
        {
            saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(ct));
        }

        using var after = await client.GetAsync(SearchUrl(setup.PublicFormId, setup.UnusedListId), ct);
        var body = await after.Content.ReadAsStringAsync(ct);
        after.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain(UNUSED_VALUE);
    }

    /// <summary>
    /// Every definition save evicts the form's cached scope. A refusal re-reads the scope, so only unbinding proves
    /// eviction: without it the cached scope would still let the list through.
    /// </summary>
    [Theory]
    [InlineData("POST forms/{id}/definitions")]
    [InlineData("PUT forms/{id}/definition")]
    [InlineData("PATCH forms/{id}/definition")]
    [InlineData("PUT forms/{id}/definitions/{definitionId}")]
    [InlineData("PATCH forms/{id}/definitions/{definitionId}")]
    public async Task A_list_unbound_by_a_definition_saved_after_the_token_was_issued_is_refused(string save)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);
        using (var before = await client.GetAsync(SearchUrl(setup.PublicFormId, setup.BoundListId), ct))
        {
            before.StatusCode.Should().Be(HttpStatusCode.OK, "the read warms the token's caches");
        }

        using var staff = await setup.World.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var json = Definition(Question("other", $"\"{setup.UnusedListId}\""));
        using (var saved = await SaveDefinitionAsync(staff, save, setup, json, ct))
        {
            saved.IsSuccessStatusCode.Should().BeTrue(await saved.Content.ReadAsStringAsync(ct));
        }

        using var after = await client.GetAsync(SearchUrl(setup.PublicFormId, setup.BoundListId), ct);
        var body = await after.Content.ReadAsStringAsync(ct);
        body.Should().NotContain(BOUND_VALUE);
        after.StatusCode.Should().Be(HttpStatusCode.NotFound, body);
    }

    private static Task<HttpResponseMessage> SaveDefinitionAsync(
        HttpClient staff,
        string save,
        Setup setup,
        string json,
        CancellationToken ct)
    {
        var forms = $"/api/forms/{setup.PublicFormId}";
        var byId = $"{forms}/definitions/{setup.ActiveDefinitionId}";
        var body = new { isDraft = false, jsonData = json };
        return save switch
        {
            "POST forms/{id}/definitions" => staff.PostAsJsonAsync($"{forms}/definitions", body, ct),
            "PUT forms/{id}/definition" => staff.PutAsJsonAsync($"{forms}/definition", body, ct),
            "PATCH forms/{id}/definition" => staff.PatchAsJsonAsync($"{forms}/definition", body, ct),
            "PUT forms/{id}/definitions/{definitionId}" => staff.PutAsJsonAsync(byId, body, ct),
            "PATCH forms/{id}/definitions/{definitionId}" => staff.PatchAsJsonAsync(byId, body, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(save), save, null),
        };
    }

    [Fact]
    public async Task A_list_of_another_tenant_stays_out_of_reach_even_when_the_form_binds_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = "Password123!" }, ct);
        var tenantId = world.Tenants[0].Id;

        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var foreign = NewList(world.Tenants[1].Id, "Foreign", OTHER_TENANT_VALUE, "Other tenant label");
        db.DataLists.Add(foreign);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Binds a foreign list", IsEnabled: true, IsPublic: true));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);
        form.AddFormDefinition(new FormDefinition(tenantId, isDraft: false, jsonData: Definition(Question("pick", $"\"{foreign.Id}\""))));
        await db.SaveChangesAsync(ct);

        using var client = await AnonymousWithFormTokenAsync(world, form.Id, ct);

        using var search = await client.GetAsync(SearchUrl(form.Id, foreign.Id), ct);
        (await search.Content.ReadAsStringAsync(ct)).Should().NotContain(OTHER_TENANT_VALUE);
        await ShouldLookLikeAMissingListAsync(client, search, SearchUrl(form.Id, MISSING_LIST_ID), ct);

        using var display = await client.GetAsync(DisplayValuesUrl(form.Id, foreign.Id, OTHER_TENANT_VALUE), ct);
        (await display.Content.ReadAsStringAsync(ct)).Should().NotContain("Other tenant label");
        await ShouldLookLikeAMissingListAsync(client, display, DisplayValuesUrl(form.Id, MISSING_LIST_ID, OTHER_TENANT_VALUE), ct);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/items")]
    public async Task A_form_token_is_not_accepted_by_the_staff_data_list_routes(string suffix)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await PrepareAsync(ct);
        using var client = await AnonymousWithFormTokenAsync(setup.World, setup.PublicFormId, ct);

        using var response = await client.GetAsync($"/api/data-lists/{setup.OtherFormListId}{suffix}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        body.Should().NotContain(OTHER_FORM_VALUE);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    private static async Task ShouldLookLikeAMissingListAsync(
        HttpClient client,
        HttpResponseMessage refused,
        string missingUrl,
        CancellationToken ct)
    {
        using var missing = await client.GetAsync(missingUrl, ct);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound, "a list id that does not exist is the reference");

        var refusedBody = await refused.Content.ReadAsStringAsync(ct);
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound, refusedBody);
        ProblemShape(refusedBody).Should().Be(ProblemShape(await missing.Content.ReadAsStringAsync(ct)));
    }

    private static string ProblemShape(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return $"{Read(root, "title")}|{Read(root, "detail")}|{Read(root, "status")}";
    }

    private static string? Read(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.ToString() : null;

    internal static string SearchUrl(long formId, long dataListId) =>
        $"/api/public/forms/{formId}/data-lists/{dataListId}/search?take=25";

    internal static string DisplayValuesUrl(long formId, long dataListId, string value) =>
        $"/api/public/forms/{formId}/data-lists/{dataListId}/display-values?values={value}";

    private async Task<Setup> PrepareAsync(CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = "Password123!" }, ct);
        var tenantId = world.Tenants[0].Id;
        var otherTenantId = world.Tenants[1].Id;

        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bound = NewList(tenantId, "Bound", BOUND_VALUE, "Bound label");
        var otherForm = NewList(tenantId, "OtherForm", OTHER_FORM_VALUE, "Other form label");
        var unused = NewList(tenantId, "Unused", UNUSED_VALUE, "Unused label");
        var draftOnly = NewList(tenantId, "DraftOnly", DRAFT_VALUE, "Draft label");
        var olderOnly = NewList(tenantId, "OlderOnly", OLDER_VALUE, "Older label");
        var otherTenant = NewList(otherTenantId, "OtherTenant", OTHER_TENANT_VALUE, "Other tenant label");
        db.DataLists.AddRange(bound, otherForm, unused, draftOnly, olderOnly, otherTenant);
        await db.SaveChangesAsync(ct);

        var publicForm = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Public form", IsEnabled: true, IsPublic: true));
        var privateForm = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Private form", IsEnabled: true, IsPublic: false));
        db.Forms.AddRange(publicForm, privateForm);
        await db.SaveChangesAsync(ct);

        // The public form's first definition, superseded below: an older definition.
        publicForm.AddFormDefinition(new FormDefinition(tenantId, isDraft: false, jsonData: Definition(Question("older", $"\"{olderOnly.Id}\""))));
        privateForm.AddFormDefinition(new FormDefinition(tenantId, isDraft: false, jsonData: Definition(Question("pick", $"\"{otherForm.Id}\""))));
        await db.SaveChangesAsync(ct);

        var active = new FormDefinition(tenantId, isDraft: false, jsonData: Definition(Question("pick", $"\"{bound.Id}\"")));
        publicForm.AddFormDefinition(active);
        publicForm.SetActiveFormDefinition(active);
        await db.SaveChangesAsync(ct);

        // Definitions of the public form that are not active: display values reach their lists, search does not.
        publicForm.AddFormDefinition(new FormDefinition(tenantId, isDraft: true, jsonData: Definition(Question("draft", $"\"{draftOnly.Id}\""))));
        await db.SaveChangesAsync(ct);

        return new Setup(world, publicForm.Id, active.Id, bound.Id, otherForm.Id, unused.Id, draftOnly.Id, olderOnly.Id, otherTenant.Id);
    }

    internal static DataList NewList(long tenantId, string name, string value, string label)
    {
        var list = new DataList(tenantId, $"{name}-{Guid.NewGuid():N}");
        list.AddItem(label, value);
        return list;
    }

    /// <summary>A definition with the given elements on one page, the way Hub saves one.</summary>
    internal static string Definition(string elements) =>
        $$"""{"pages":[{"name":"page1","elements":[{{elements}}]}]}""";

    /// <summary>A question bound to a list the way Hub binds one: <c>edxDataListId</c>.</summary>
    internal static string Question(string name, string id) =>
        $$"""{"type":"dropdown","name":"{{name}}","edxDataListId":{{id}}}""";

    private static string Placement(string placement, string id) => placement switch
    {
        "nested-panel" =>
            $$"""{"type":"panel","name":"outer","elements":[{"type":"panel","name":"inner","elements":[{{Question("q", id)}}]}]}""",
        "dynamic-panel" =>
            $$"""{"type":"paneldynamic","name":"dp","templateElements":[{{Question("q", id)}}]}""",
        "matrix-column" =>
            $$"""{"type":"matrixdropdown","name":"m","rows":["r1"],"columns":[{"name":"c","cellType":"dropdown","edxDataListId":{{id}}}]}""",
        "matrix-dynamic-column" =>
            $$"""{"type":"matrixdynamic","name":"md","columns":[{"name":"c","cellType":"dropdown","edxDataListId":{{id}}}]}""",
        _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, null),
    };

    internal static async Task<HttpClient> AnonymousWithFormTokenAsync(
        IntegrationTestWorld world,
        long formId,
        CancellationToken ct)
    {
        var client = world.AnonymousClient();
        var token = await MintAsync(client, formId, ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    internal static async Task<string> MintAsync(HttpClient client, long formId, CancellationToken ct)
    {
        using var minted = await client.PostAsJsonAsync($"/api/public/forms/{formId}/access-tokens", new { }, ct);
        var mintedBody = await minted.Content.ReadAsStringAsync(ct);
        minted.StatusCode.Should().Be(HttpStatusCode.OK, mintedBody);
        return (await minted.Content.ReadFromJsonAsync<TokenResponse>(ct))!.Token;
    }

    private sealed record TokenResponse(string Token, DateTime ExpiresAtUtc);

    private sealed record Setup(
        IntegrationTestWorld World,
        long PublicFormId,
        long ActiveDefinitionId,
        long BoundListId,
        long OtherFormListId,
        long UnusedListId,
        long DraftOnlyListId,
        long OlderOnlyListId,
        long OtherTenantListId)
    {
        public long ListId(string name) => name switch
        {
            nameof(OtherFormListId) => OtherFormListId,
            nameof(UnusedListId) => UnusedListId,
            nameof(DraftOnlyListId) => DraftOnlyListId,
            nameof(OlderOnlyListId) => OlderOnlyListId,
            nameof(OtherTenantListId) => OtherTenantListId,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }
}
