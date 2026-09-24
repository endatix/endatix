using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests.FeatureFlows.Api;

/// <summary>
/// A page past the end, up to <c>int.MaxValue</c>, answers with the last page on every paged list,
/// against the real database OFFSET (endatix/endatix#1075).
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
public sealed class PagedListWindowFlowTests
{
    private const string HugePage = "page=2147483647&pageSize=1";

    private readonly EndatixIntegrationWebHostFixture _fixture;

    public PagedListWindowFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> TenantLists =>
        ["forms", "themes", "users", "roles", "data-lists", "form-templates", "questions"];

    public static TheoryData<string> PlatformLists => ["admin/tenants", "admin/platform-admins"];

    [Theory]
    [MemberData(nameof(TenantLists))]
    public async Task Tenant_list_with_huge_page_returns_the_last_page(string route)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await ClientAsAsync(TestPersona.TenantAdmin, cancellationToken);

        // Act
        var page = await GetPageAsync(client, $"/api/{route}?{HugePage}", cancellationToken);

        // Assert
        AssertIsLastPage(page);
    }

    [Theory]
    [MemberData(nameof(PlatformLists))]
    public async Task Platform_list_with_huge_page_returns_the_last_page(string route)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await ClientAsAsync(TestPersona.PlatformAdmin, cancellationToken);

        // Act
        var page = await GetPageAsync(client, $"/api/{route}?{HugePage}", cancellationToken);

        // Assert
        AssertIsLastPage(page);
    }

    [Fact]
    public async Task Submissions_page_past_the_end_returns_the_last_page()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await PrepareWorldAsync(cancellationToken);
        await EnsureTenantSettingsAsync(world, cancellationToken);
        using var client = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        var formId = await CreateFormWithSubmissionsAsync(client, submissions: 3, cancellationToken);

        // Act
        var pastTheEnd = await GetPageAsync(client, $"/api/forms/{formId}/submissions?page=5&pageSize=2", cancellationToken);
        var huge = await GetPageAsync(client, $"/api/forms/{formId}/submissions?page=2147483647&pageSize=2", cancellationToken);

        // Assert
        Assert.Equal(new ListPage(2, 2, 3, 1), pastTheEnd);
        Assert.Equal(pastTheEnd, huge);
    }

    private static void AssertIsLastPage(ListPage page)
    {
        if (page.TotalRecords == 0)
        {
            Assert.Equal(new ListPage(1, 0, 0, 0), page);
            return;
        }

        Assert.Equal(page.TotalPages, page.Page);
        Assert.Equal(1, page.ItemCount);
    }

    private async Task<HttpClient> ClientAsAsync(TestPersona persona, CancellationToken cancellationToken)
    {
        var world = await PrepareWorldAsync(cancellationToken);
        return await world.AsAsync(persona, cancellationToken: cancellationToken);
    }

    private Task<IntegrationTestWorld> PrepareWorldAsync(CancellationToken cancellationToken) =>
        _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = "Password123!" },
            cancellationToken);

    /// <summary>Creating a submission reads tenant settings, which the standard seed does not add.</summary>
    private static async Task EnsureTenantSettingsAsync(IntegrationTestWorld world, CancellationToken cancellationToken)
    {
        var tenantId = world.Tenants[0].Id;
        await using var scope = world.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.TenantSettings.AnyAsync(row => row.TenantId == tenantId, cancellationToken))
        {
            db.TenantSettings.Add(new TenantSettings(tenantId));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<ListPage> GetPageAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = body.RootElement;
        return new ListPage(
            root.GetProperty("page").GetInt32(),
            root.GetProperty("totalPages").GetInt32(),
            root.GetProperty("totalRecords").GetInt32(),
            root.GetProperty("items").GetArrayLength());
    }

    private static async Task<string> CreateFormWithSubmissionsAsync(
        HttpClient client,
        int submissions,
        CancellationToken cancellationToken)
    {
        var formResponse = await client.PostAsJsonAsync(
            "/api/forms",
            new
            {
                name = $"paged-list-form-{Guid.NewGuid():N}",
                isEnabled = true,
                formDefinitionJsonData = """{"pages":[{"name":"page1","elements":[{"type":"text","name":"q1"}]}]}"""
            },
            cancellationToken);
        formResponse.EnsureSuccessStatusCode();
        using var form = JsonDocument.Parse(await formResponse.Content.ReadAsStringAsync(cancellationToken));
        var formId = form.RootElement.GetProperty("id").GetString()!;

        for (var i = 0; i < submissions; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/forms/{formId}/submissions",
                new { isComplete = true, jsonData = $$"""{"q1":"answer-{{i}}"}""" },
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        return formId;
    }

    private sealed record ListPage(int Page, int TotalPages, int TotalRecords, int ItemCount);
}
