using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using static Endatix.IntegrationTests.FeatureFlows.DataLists.PublicDataListScopeFlowTests;

namespace Endatix.IntegrationTests.FeatureFlows.DataLists;

/// <summary>
/// A form access token is limited per client, and is checked against the form's current state on every use:
/// once its form is made private, disabled or deleted, an anonymously issued token reads nothing.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P0")]
public sealed class FormAccessTokenUseFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string BOUND_VALUE = "bound-choice";

    private static readonly string[] _submitPermission = ["submit"];

    [Theory]
    [InlineData("private", HttpStatusCode.Forbidden)]
    [InlineData("disabled", HttpStatusCode.NotFound)]
    [InlineData("deleted", HttpStatusCode.NotFound)]
    public async Task An_anonymous_token_is_refused_once_its_form_is_closed(string change, HttpStatusCode refusal)
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, formId, listId) = await PrepareAsync(isPublic: true, ct);
        using var client = await AnonymousWithFormTokenAsync(world, formId, ct);

        // Warm every cache the token path uses, so the refusal below proves eviction.
        await ShouldReadAsync(client, formId, listId, ct);

        using var staff = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        using (var closed = await CloseAsync(staff, formId, change, ct))
        {
            closed.IsSuccessStatusCode.Should().BeTrue(await closed.Content.ReadAsStringAsync(ct));
        }

        using var search = await client.GetAsync(SearchUrl(formId, listId), ct);
        var searchBody = await search.Content.ReadAsStringAsync(ct);
        searchBody.Should().NotContain(BOUND_VALUE);
        search.StatusCode.Should().Be(refusal, searchBody);

        using var display = await client.GetAsync(DisplayValuesUrl(formId, listId, BOUND_VALUE), ct);
        var displayBody = await display.Content.ReadAsStringAsync(ct);
        displayBody.Should().NotContain("Bound label");
        display.StatusCode.Should().Be(refusal, displayBody);
    }

    [Fact]
    public async Task A_token_issued_to_staff_for_a_private_form_reads_that_forms_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, formId, listId) = await PrepareAsync(isPublic: false, ct);

        using var staff = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var token = await MintAsync(staff, formId, ct);

        using var runtime = world.AnonymousClient();
        runtime.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await ShouldReadAsync(runtime, formId, listId, ct);
    }

    [Fact]
    public async Task A_token_issued_for_a_share_link_reads_its_private_forms_list()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, formId, listId) = await PrepareAsync(isPublic: false, ct);
        using var staff = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: ct);
        var shareToken = await ShareLinkTokenAsync(staff, formId, ct);

        using var runtime = world.AnonymousClient();
        using var minted = await runtime.PostAsJsonAsync(
            $"/api/public/forms/{formId}/access-tokens",
            new { token = shareToken, tokenType = "AccessToken" },
            ct);
        var mintedBody = await minted.Content.ReadAsStringAsync(ct);
        minted.StatusCode.Should().Be(HttpStatusCode.OK, mintedBody);
        var token = JsonDocument.Parse(mintedBody).RootElement.GetProperty("token").GetString();

        // A public grant on a private form is refused (403), so a 200 here proves the private grant.
        runtime.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await ShouldReadAsync(runtime, formId, listId, ct);
    }

    [Fact]
    public async Task Anonymous_callers_cannot_issue_a_token_for_a_private_form()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, formId, _) = await PrepareAsync(isPublic: false, ct);
        using var client = world.AnonymousClient();

        using var minted = await client.PostAsJsonAsync($"/api/public/forms/{formId}/access-tokens", new { }, ct);

        minted.StatusCode.Should().Be(HttpStatusCode.Unauthorized, await minted.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Issuing_tokens_is_limited_per_client()
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, formId, _) = await PrepareAsync(isPublic: true, ct);
        using var client = world.AnonymousClient();

        for (var i = 0; i < Endatix.Api.Endpoints.Public.Forms.CreateFormAccessToken.REQUESTS_PER_MINUTE; i++)
        {
            using var allowed = await client.PostAsJsonAsync($"/api/public/forms/{formId}/access-tokens", new { }, ct);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} is within the limit");
        }

        using var limited = await client.PostAsJsonAsync($"/api/public/forms/{formId}/access-tokens", new { }, ct);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        using var otherClient = world.AnonymousClient();
        using var other = await otherClient.PostAsJsonAsync($"/api/public/forms/{formId}/access-tokens", new { }, ct);
        other.StatusCode.Should().Be(HttpStatusCode.OK, "the limit is per client");
    }

    [Theory]
    [InlineData("search")]
    [InlineData("display-values")]
    public async Task Public_data_list_reads_are_limited_per_client(string route)
    {
        var ct = TestContext.Current.CancellationToken;
        var (world, formId, listId) = await PrepareAsync(isPublic: true, ct);
        using var client = await AnonymousWithFormTokenAsync(world, formId, ct);
        var url = route == "search" ? SearchUrl(formId, listId) : DisplayValuesUrl(formId, listId, BOUND_VALUE);
        var limit = route == "search"
            ? Endatix.Api.Endpoints.Public.DataLists.Search.REQUESTS_PER_MINUTE
            : Endatix.Api.Endpoints.Public.DataLists.GetChoiceDisplayValues.REQUESTS_PER_MINUTE;

        for (var i = 0; i < limit; i++)
        {
            using var allowed = await client.GetAsync(url, ct);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK, $"request {i + 1} is within the limit");
        }

        using var limited = await client.GetAsync(url, ct);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        using var otherClient = await AnonymousWithFormTokenAsync(world, formId, ct);
        using var other = await otherClient.GetAsync(url, ct);
        other.StatusCode.Should().Be(HttpStatusCode.OK, "the limit is per client");
    }

    /// <summary>A share link's access token for a submission staff made on the form.</summary>
    private static async Task<string> ShareLinkTokenAsync(HttpClient staff, long formId, CancellationToken ct)
    {
        using var created = await staff.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions",
            new { isComplete = false, jsonData = "{}" },
            ct);
        var createdBody = await created.Content.ReadAsStringAsync(ct);
        created.IsSuccessStatusCode.Should().BeTrue(createdBody);
        var submissionId = JsonDocument.Parse(createdBody).RootElement.GetProperty("id").GetString();

        using var issued = await staff.PostAsJsonAsync(
            $"/api/forms/{formId}/submissions/{submissionId}/access-token",
            new { expiryMinutes = 60, permissions = _submitPermission },
            ct);
        var issuedBody = await issued.Content.ReadAsStringAsync(ct);
        issued.IsSuccessStatusCode.Should().BeTrue(issuedBody);
        return JsonDocument.Parse(issuedBody).RootElement.GetProperty("token").GetString()!;
    }

    private static async Task ShouldReadAsync(HttpClient client, long formId, long listId, CancellationToken ct)
    {
        using var search = await client.GetAsync(SearchUrl(formId, listId), ct);
        var body = await search.Content.ReadAsStringAsync(ct);
        search.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain(BOUND_VALUE);

        using var display = await client.GetAsync(DisplayValuesUrl(formId, listId, BOUND_VALUE), ct);
        display.StatusCode.Should().Be(HttpStatusCode.OK, await display.Content.ReadAsStringAsync(ct));
    }

    private static Task<HttpResponseMessage> CloseAsync(HttpClient staff, long formId, string change, CancellationToken ct) =>
        change switch
        {
            "private" => staff.PatchAsJsonAsync($"/api/forms/{formId}", new { isPublic = false }, ct),
            "disabled" => staff.PatchAsJsonAsync($"/api/forms/{formId}", new { isEnabled = false }, ct),
            "deleted" => staff.DeleteAsync($"/api/forms/{formId}", ct),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };

    private async Task<(IntegrationTestWorld World, long FormId, long ListId)> PrepareAsync(bool isPublic, CancellationToken ct)
    {
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" }, ct);
        var (form, list) = await SeedRuntimeFormAsync(world, isPublic, ct);
        return (world, form, list);
    }

    internal static async Task<(long Form, long List)> SeedRuntimeFormAsync(IntegrationTestWorld world, bool isPublic, CancellationToken ct)
    {
        var tenantId = world.Tenants[0].Id;

        using var scope = world.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var list = NewList(tenantId, "Bound", BOUND_VALUE, "Bound label");
        db.DataLists.Add(list);
        var form = Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Runtime form", IsEnabled: true, IsPublic: isPublic));
        db.Forms.Add(form);
        await db.SaveChangesAsync(ct);

        form.AddFormDefinition(new FormDefinition(tenantId, isDraft: false, jsonData: Definition(Question("pick", $"\"{list.Id}\""))));
        await db.SaveChangesAsync(ct);

        return (form.Id, list.Id);
    }
}
