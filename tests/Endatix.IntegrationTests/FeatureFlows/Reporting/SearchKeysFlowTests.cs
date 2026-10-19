using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// <c>POST</c> and <c>DELETE forms/{formId}/reporting/search-keys</c> over HTTP on <see cref="ReportingWebHost"/>: the
/// response and columns the Hub reads, the caps, the key in the query string, who may change the keys, and a filter on
/// a new key once its rebuild ran.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class SearchKeysFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string ReaderRole = "SearchKeysReader";
    private const string ReaderUserName = "search-keys-reader";
    private const string KeyWithSlash = "ref/code";

    // Thirty questions the compile makes searchable, and read-only ones only a person can add.
    private static readonly string Definition = DefinitionWithReadOnly(KeyWithSlash);

    [Fact]
    public async Task Adding_a_key_answers_at_once_with_the_key_pending_and_the_columns_show_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, IntegrationWorldOptions.SingleTenant, ct);
        var formId = await CreatePreparedFormAsync(client, """{"r001":"ALPHA-1"}""", ct);

        // Act
        using var added = await AddAsync(client, formId, "r001", ct);

        // Assert
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        using var keys = JsonDocument.Parse(await added.Content.ReadAsStringAsync(ct));
        var key = Key(keys.RootElement, "r001");
        (key.GetProperty("source").GetString(), key.GetProperty("pending").GetBoolean()).Should().Be(("user", true));
        using var columns = await GetOkAsync(client, $"/api/forms/{formId}/reporting/columns", ct);
        var column = Column(columns.RootElement, "r001");
        (column.GetProperty("searchable").GetBoolean(), column.GetProperty("searchPending").GetBoolean(),
            column.GetProperty("searchSource").GetString()).Should().Be((true, true, "user"));
        columns.RootElement.GetProperty("searchSlots").GetRawText().Should()
            .Be("""{"used":31,"max":64,"userUsed":1,"userMax":14}""");
    }

    [Fact]
    public async Task After_its_rebuild_a_filter_on_the_new_key_returns_the_match()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, IntegrationWorldOptions.SingleTenant, ct);
        var formId = await CreatePreparedFormAsync(client, """{"r001":"ALPHA-1"}""", ct);
        (await AddAsync(client, formId, "r001", ct)).Dispose();

        // Act
        await RunRebuildRequestsAsync(host, ct);

        // Assert
        using var page = await GetOkAsync(
            client, $"/api/forms/{formId}/reporting/submissions?answer={Uri.EscapeDataString("contains:r001:alpha")}", ct);
        page.RootElement.GetProperty("totalRecords").GetInt32().Should().Be(1);
        using var columns = await GetOkAsync(client, $"/api/forms/{formId}/reporting/columns", ct);
        Column(columns.RootElement, "r001").GetProperty("searchPending").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_fifteenth_user_key_is_a_bad_request_naming_the_caps()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, IntegrationWorldOptions.SingleTenant, ct);
        var formId = await CreateCompiledFormAsync(client, ct);
        foreach (var name in SearchCopyForms.ReadOnlyNames(FormSearchKeys.MaxUserKeys))
        {
            (await AddAsync(client, formId, name, ct)).EnsureSuccessStatusCode();
        }

        // Act
        using var fifteenth = await AddAsync(client, formId, KeyWithSlash, ct);

        // Assert
        fifteenth.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fifteenth.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        using var problem = JsonDocument.Parse(await fifteenth.Content.ReadAsStringAsync(ct));
        problem.RootElement.GetProperty("detail").GetString().Should().Contain("14").And.Contain("64");
    }

    [Fact]
    public async Task A_key_with_a_slash_is_removed_through_the_query_string()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, IntegrationWorldOptions.SingleTenant, ct);
        var formId = await CreateCompiledFormAsync(client, ct);
        (await AddAsync(client, formId, KeyWithSlash, ct)).EnsureSuccessStatusCode();

        // Act
        using var removed = await RemoveAsync(client, formId, KeyWithSlash, ct);

        // Assert
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var keys = JsonDocument.Parse(await removed.Content.ReadAsStringAsync(ct));
        keys.RootElement.GetProperty("keys").EnumerateArray()
            .Select(key => key.GetProperty("key").GetString()).Should().NotContain(KeyWithSlash);
    }

    [Fact]
    public async Task Removing_an_automatic_key_is_a_bad_request()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        using var client = await TenantAdminAsync(host, IntegrationWorldOptions.SingleTenant, ct);
        var formId = await CreateCompiledFormAsync(client, ct);

        // Act
        using var removed = await RemoveAsync(client, formId, "q001", ct);

        // Assert
        removed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_caller_who_may_not_edit_forms_cannot_change_the_keys()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, ct);
        using var admin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var formId = await CreateCompiledFormAsync(admin, ct);
        using var reader = await ReaderAsync(host, world, ct);

        // Act
        using var added = await AddAsync(reader, formId, "r001", ct);
        using var removed = await RemoveAsync(reader, formId, "q001", ct);

        // Assert
        (added.StatusCode, removed.StatusCode).Should().Be((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task Another_tenants_form_is_not_found()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using ReportingWebHost host = new(fixture);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant with { DefaultPassword = SeedPassword }, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 0, cancellationToken: ct);
        using var stranger = await world.AsAsync(TestPersona.TenantAdmin, tenantIndex: 1, cancellationToken: ct);
        var formId = await CreateCompiledFormAsync(owner, ct);
        (await AddAsync(owner, formId, "r001", ct)).EnsureSuccessStatusCode();

        // Act
        using var added = await AddAsync(stranger, formId, "r002", ct);
        using var removed = await RemoveAsync(stranger, formId, "r001", ct);

        // Assert
        (added.StatusCode, removed.StatusCode).Should().Be((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
    }

    private static string DefinitionWithReadOnly(string extraReadOnlyName)
    {
        var definition = JsonNode.Parse(SearchCopyForms.TextQuestions(30, readOnly: FormSearchKeys.MaxUserKeys))!;
        definition["pages"]![0]!["elements"]!.AsArray()
            .Add(new JsonObject { ["type"] = "text", ["name"] = extraReadOnlyName, ["readOnly"] = true });
        return definition.ToJsonString();
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, string formId, string key, CancellationToken ct) =>
        client.PostAsJsonAsync($"/api/forms/{formId}/reporting/search-keys", new { key }, ct);

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient client, string formId, string key, CancellationToken ct) =>
        client.DeleteAsync($"/api/forms/{formId}/reporting/search-keys?key={Uri.EscapeDataString(key)}", ct);

    // The host's outbox relay is closed (see ReportingWebHost), so the requests run here as the relay would run them.
    private static async Task RunRebuildRequestsAsync(ReportingWebHost host, CancellationToken ct)
    {
        using var scope = host.Services.CreateScope();
        var requests = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.EventType == SearchCopyRebuildRequests.EventTypeName)
            .ToListAsync(ct);
        var handler = scope.ServiceProvider.GetRequiredService<RebuildSearchCopyOutboxHandler>();
        foreach (var request in requests)
        {
            await handler.HandleAsync(new OutboxMessageView(request), ct);
        }
    }

    private static async Task<HttpClient> TenantAdminAsync(ReportingWebHost host, IntegrationWorldOptions options, CancellationToken ct)
    {
        var world = await host.PrepareWorldAsync(options with { DefaultPassword = SeedPassword }, ct);
        return await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
    }

    // A user whose only role may view forms but not edit them.
    private static async Task<HttpClient> ReaderAsync(ReportingWebHost host, IntegrationTestWorld world, CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;
        await host.Seed.SeedRoleAsync(tenantId, ReaderRole, ct);
        await host.Seed.SeedRolePermissionsAsync(tenantId, ReaderRole, [Actions.Access.Hub, Actions.Forms.View], ct);
        await host.Seed.SeedUserAsync(tenantId, ReaderUserName, $"{ReaderUserName}@test.local", password: SeedPassword, cancellationToken: ct);
        await host.Seed.SeedUserRoleAssignmentAsync(tenantId, ReaderUserName, ReaderRole, ct);
        return await world.AsAsync(TestPersona.CustomRole(ReaderRole), userName: ReaderUserName, cancellationToken: ct);
    }

    private static async Task<string> CreatePreparedFormAsync(HttpClient client, string answers, CancellationToken ct)
    {
        var formId = await CreateCompiledFormAsync(client, ct);
        using (var submitted = await client.PostAsJsonAsync(
                   $"/api/forms/{formId}/submissions", new { isComplete = true, jsonData = answers }, ct))
        {
            submitted.EnsureSuccessStatusCode();
        }

        using var backfill = await client.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/backfill", new { completionScope = "completed" }, ct);
        backfill.StatusCode.Should().Be(HttpStatusCode.OK);
        return formId;
    }

    private static async Task<string> CreateCompiledFormAsync(HttpClient client, CancellationToken ct)
    {
        using var created = await client.PostAsJsonAsync(
            "/api/forms",
            new { name = $"search-keys-{Guid.NewGuid():N}", isEnabled = true, formDefinitionJsonData = Definition },
            ct);
        created.EnsureSuccessStatusCode();
        using var form = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct));
        var formId = form.RootElement.GetProperty("id").GetString()!;
        using var compiled = await client.PostAsJsonAsync($"/api/forms/{formId}/reporting/compile-schema", new { }, ct);
        compiled.StatusCode.Should().Be(HttpStatusCode.OK);
        return formId;
    }

    private static async Task<JsonDocument> GetOkAsync(HttpClient client, string uri, CancellationToken ct)
    {
        using var response = await client.GetAsync(uri, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private static JsonElement Key(JsonElement searchKeys, string key) =>
        searchKeys.GetProperty("keys").EnumerateArray().Single(entry => entry.GetProperty("key").GetString() == key);

    private static JsonElement Column(JsonElement root, string key) =>
        root.GetProperty("columns").EnumerateArray().Single(column => column.GetProperty("key").GetString() == key);
}
