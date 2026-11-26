using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    [Fact]
    public async Task Form_assignment_uses_only_active_folders_of_the_current_tenant()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.MultiTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var other = await world.AsAsync(TestPersona.TenantAdmin, 1, IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var own = await ManagedAsset.CreateAsync(owner, "folders", ct);
        var foreign = await ManagedAsset.CreateAsync(other, "folders", ct);
        var valid = new { name = "Assigned form", isEnabled = true, folderId = own.Id, formDefinitionJsonData = """{"pages":[]}""" };
        using var created = await owner.PostAsJsonAsync("/api/forms", valid, ct);
        created.EnsureSuccessStatusCode();
        using var deactivate = await owner.PatchAsJsonAsync(own.Detail, new { isActive = false }, ct);
        deactivate.EnsureSuccessStatusCode();
        var before = await owner.GetFromJsonAsync<JsonElement>("/api/forms", ct);

        // Act
        foreach (var (id, status, detail) in new[]
        {
            (own.Id, HttpStatusCode.Conflict, "Folder is not active."),
            (foreign.Id, HttpStatusCode.NotFound, "Folder was not found."),
            ("9223372036854775807", HttpStatusCode.NotFound, "Folder was not found.")
        })
        {
            var body = valid with { name = "Rejected assignment", folderId = id };
            using var response = await owner.PostAsJsonAsync("/api/forms", body, ct);
            Assert.Equal(status, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(detail, problem.GetProperty("detail").GetString());
        }

        // Assert
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/forms", ct);
        Assert.True(JsonElement.DeepEquals(before, after));
    }

    [Theory]
    [InlineData("/api/forms")]
    [InlineData("/api/form-templates")]
    public async Task Omitted_folder_is_valid_only_while_the_tenant_allows_unassigned_content(string path)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        await EnsureTenantSettingsAsync(world, ct);
        object payload = path == "/api/forms"
            ? new { name = "Unassigned form", isEnabled = true, formDefinitionJsonData = """{"pages":[]}""" }
            : new { name = "Unassigned template", jsonData = "{}" };
        using var allowed = await owner.PostAsJsonAsync(path, payload, ct);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using var policy = await owner.PatchAsJsonAsync("/api/tenant-settings", new { requireFolderAssignment = true }, ct);
        policy.EnsureSuccessStatusCode();
        var before = await owner.GetFromJsonAsync<JsonElement>(path, ct);

        // Act
        using var refused = await owner.PostAsJsonAsync(path, payload, ct);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("FOLDER_REQUIRED", problem.GetProperty("errorCode").GetString());
        Assert.True(JsonElement.DeepEquals(before, await owner.GetFromJsonAsync<JsonElement>(path, ct)));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    public async Task Clear_standing_preserves_first_validation_error_when_assignments_are_also_invalid(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var owner = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        var route = $"/api/forms/{await CreateFormAsync(owner, ct)}";
        using var enabled = await owner.PatchAsJsonAsync(route, new { limitOnePerUser = true }, ct);
        enabled.EnsureSuccessStatusCode();
        await using var host = new ReportingWebHost(fixture, EnableStanding);
        using var clear = host.CreateClient();
        clear.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;

        foreach (var client in new[] { owner, clear })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route)
            {
                Content = JsonContent.Create(new { name = "Changed form", isEnabled = true, limitOnePerUser = false, folderId = "99999", themeId = 99999 })
            };
            using var result = await client.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
            var problem = await result.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("Single submission gate cannot be disabled after it has been enabled.", problem.GetProperty("detail").GetString());
        }
    }
}
