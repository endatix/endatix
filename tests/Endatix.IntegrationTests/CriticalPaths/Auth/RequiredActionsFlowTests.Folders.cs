using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public void Folder_edit_and_delete_describe_their_distinct_locked_outcomes()
    {
        var api = fixture.Factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
        var operations = api.ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .Where(operation => operation.RelativePath == "api/folders/{folderId}").ToArray();
        var patch = Assert.Single(operations, operation => operation.HttpMethod == "PATCH");
        var delete = Assert.Single(operations, operation => operation.HttpMethod == "DELETE");

        Assert.Contains(patch.SupportedResponseTypes, response => response.StatusCode == 400);
        Assert.DoesNotContain(patch.SupportedResponseTypes, response => response.StatusCode == 409);
        Assert.Contains(delete.SupportedResponseTypes, response => response.StatusCode == 409);
    }

    [Fact]
    public async Task Folder_operations_refuse_pending_or_unavailable_standing_and_resume_without_lost_data()
    {
        var ct = TestContext.Current.CancellationToken;
        var action = new MutableProvider("setup", "/setup") { Pending = false };
        var conflict = new MutableProvider("other", "/other") { Pending = false };
        await using var host = new ReportingWebHost(fixture, services =>
        {
            services.AddSingleton<IRequiredActionProvider>(action);
            services.AddSingleton<IRequiredActionProvider>(conflict);
            EnableStanding(services);
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var folder = await ManagedAsset.CreateAsync(owner, "folders", ct);
        var operations = FolderOperations(folder);
        action.Pending = true;
        await InvalidateAsync(world, ct);
        var me = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        foreach (var op in operations)
        {
            using var blocked = await ManagedAsset.SendAsync(owner, op, ct);
            await AssertStandingRefusalAsync(blocked, me, ct);
        }
        conflict.Pending = true;
        await InvalidateAsync(world, ct);
        foreach (var op in operations)
        {
            using var unavailable = await ManagedAsset.SendAsync(owner, op, ct);
            await AssertUnavailableQuestionsAsync(unavailable, ct);
        }
        action.Pending = conflict.Pending = false;
        await InvalidateAsync(world, ct);
        var rows = await owner.GetFromJsonAsync<JsonElement>(folder.Path, ct);
        Assert.Equal("Original", Assert.Single(rows.EnumerateArray()).GetProperty("name").GetString());
        await AssertResumedFoldersAsync(owner, operations);
    }

    private static async Task AssertResumedFoldersAsync(HttpClient owner, (string, string)[] operations)
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var op in operations)
        {
            using var resumed = await ManagedAsset.SendAsync(owner, op, ct);
            resumed.EnsureSuccessStatusCode();
        }
    }

    [Theory]
    [InlineData(true, RequiredActionSeverity.Notice)]
    [InlineData(true, null)]
    [InlineData(false, RequiredActionSeverity.Blocking)]
    public async Task Folder_operations_allow_notices_no_provider_and_dormant_hosts(bool enforce, RequiredActionSeverity? severity)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = new ReportingWebHost(fixture, services =>
        {
            if (severity.HasValue)
            {
                services.AddSingleton<IRequiredActionProvider>(new MutableProvider("setup", "/setup") { Severity = severity.Value });
            }
            if (enforce)
            {
                EnableStanding(services);
            }
        });
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var folder = await ManagedAsset.CreateAsync(owner, "folders", ct);
        await AssertResumedFoldersAsync(owner, FolderOperations(folder));
    }

    private static (string Method, string Route)[] FolderOperations(ManagedAsset folder) =>
        [("GET", folder.Path), ("GET", folder.Path + "?includeInactive=true"), ("GET", folder.Path + "/by-slug/original"),
         ("POST", folder.Path), ("PATCH", folder.Detail), ("DELETE", folder.Detail)];

    [Fact]
    public async Task Folder_admission_preserves_validation_and_visibility_before_standing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var neighbor = await ManagedAsset.CreateAsync(other, "folders", ct);
        using var hidden = await client.GetAsync("/api/folders/by-slug/original", ct);
        using var edit = await client.PatchAsJsonAsync(neighbor.Detail, new { name = "Changed" }, ct);
        using var delete = await client.DeleteAsync(neighbor.Detail, ct);
        Assert.All(new[] { hidden, edit, delete }, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        var own = await ManagedAsset.CreateAsync(owner, "folders", ct);
        using var locked = await owner.PatchAsJsonAsync(own.Detail, new { immutable = true, isActive = false }, ct);
        locked.EnsureSuccessStatusCode();
        var all = await owner.GetFromJsonAsync<JsonElement>(own.Path + "?includeInactive=true", ct);
        Assert.Equal(own.Id, Assert.Single(all.EnumerateArray()).GetProperty("id").GetString());
        using var inactive = await client.GetAsync("/api/folders/by-slug/original", ct);
        using var duplicate = await client.PostAsJsonAsync(own.Path, new { name = "Original" }, ct);
        using var invalid = await client.PatchAsJsonAsync(own.Detail, new { name = "Changed" }, ct);
        using var conflict = await client.DeleteAsync(own.Detail, ct);
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Empty(snapshots.ReceivedCalls());
        using var unlocked = await client.PatchAsJsonAsync(own.Detail, new { immutable = false, name = "Changed" }, ct);
        await AssertUnavailableQuestionsAsync(unlocked, ct);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("PATCH", HttpStatusCode.BadRequest)]
    [InlineData("DELETE", HttpStatusCode.Conflict)]
    public async Task Folder_writes_honor_a_lock_committed_during_admission(string method, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var folder = await ManagedAsset.CreateAsync(owner, "folders", ct);
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            using var locked = await owner.PatchAsJsonAsync(folder.Detail, new { immutable = true }, ct);
            locked.EnsureSuccessStatusCode();
            return Result.Success(new AuthorizationData());
        });
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        using var write = await ManagedAsset.SendAsync(client, (method, folder.Detail), ct);
        Assert.Equal(expected, write.StatusCode);
        var unchanged = await owner.GetFromJsonAsync<JsonElement>("/api/folders/by-slug/original", ct);
        Assert.True(unchanged.GetProperty("immutable").GetBoolean());
        Assert.Equal("Original", unchanged.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Folder_inactive_listing_keeps_the_ordinary_manage_permission_decision()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        const string role = "FolderReader";
        const string name = "folder-reader@example.test";
        var tenant = world.Tenants[0].Id;
        await world.Seed.SeedRoleAsync(tenant, role, ct);
        await world.Seed.SeedRolePermissionsAsync(tenant, role, [Actions.Forms.View], ct);
        await world.Seed.SeedUserAsync(tenant, name, name, cancellationToken: ct);
        await world.Seed.SeedUserRoleAssignmentAsync(tenant, name, role, ct);
        using var reader = await world.AsAsync(TestPersona.CustomRole(role), mode: IntegrationAuthMode.SyntheticJwt, userName: name, cancellationToken: ct);
        const string inactive = "/api/folders?includeInactive=true";
        using var permitted = await reader.GetAsync("/api/folders", ct);
        Assert.Equal(HttpStatusCode.OK, permitted.StatusCode);
        using var ordinary = await reader.GetAsync(inactive, ct);
        await AssertOrdinaryFolderDenialAsync(ordinary, ct);

        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AuthorizationData { RequiredActionsUnavailable = true }));
        await using var host = new ReportingWebHost(fixture, services => services.AddScoped(_ => new AccountStanding(snapshots)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = reader.DefaultRequestHeaders.Authorization;
        using var denied = await client.GetAsync(inactive, ct);
        await AssertOrdinaryFolderDenialAsync(denied, ct);
        Assert.Empty(snapshots.ReceivedCalls());
        using var active = await client.GetAsync("/api/folders", ct);
        await AssertUnavailableQuestionsAsync(active, ct);
    }

    private static async Task AssertOrdinaryFolderDenialAsync(HttpResponseMessage response, CancellationToken ct)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(problem.TryGetProperty("requiredActions", out _));
        Assert.False(problem.TryGetProperty("items", out _));
        Assert.False(problem.TryGetProperty("name", out _));
    }
}
