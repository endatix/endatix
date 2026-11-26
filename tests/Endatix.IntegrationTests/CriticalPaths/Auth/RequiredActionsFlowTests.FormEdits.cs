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
        foreach (var id in new[] { own.Id, foreign.Id })
        {
            var body = valid with { name = "Rejected assignment", folderId = id };
            using var response = await owner.PostAsJsonAsync("/api/forms", body, ct);
            Assert.False(response.IsSuccessStatusCode);
        }

        // Assert
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/forms", ct);
        Assert.True(JsonElement.DeepEquals(before, after));
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
