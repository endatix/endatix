using System.Net.Http.Json;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class AuthLoginFlowTests
{
    private const string SEED_PASSWORD = "Password123!";
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public AuthLoginFlowTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_and_me_return_expected_roles_and_permissions_for_seeded_role_users()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var world = await _fixture.PrepareWorldAsync(
            IntegrationWorldOptions.MultiTenant with { DefaultPassword = SEED_PASSWORD },
            cancellationToken);

        var tenant = world.Tenants[0];

        // Act & Assert — TenantAdmin
        using var adminClient = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var adminData = await GetMeAsync(adminClient, cancellationToken);
        Assert.Equal(tenant.Id, adminData.TenantId);
        Assert.Contains(SystemRole.Authenticated.Name, adminData.Roles);
        Assert.Contains(SystemRole.Admin.Name, adminData.Roles);
        Assert.Contains(Actions.Access.Authenticated, adminData.Permissions);
        Assert.Contains(Actions.Access.Hub, adminData.Permissions);
        Assert.True(adminData.IsAdmin);

        // Act & Assert — Creator
        using var creatorClient = await world.AsAsync(TestPersona.Creator, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var creatorData = await GetMeAsync(creatorClient, cancellationToken);
        Assert.Equal(tenant.Id, creatorData.TenantId);
        Assert.Contains(SystemRole.Creator.Name, creatorData.Roles);
        Assert.Contains(Actions.Forms.Create, creatorData.Permissions);

        // Act & Assert — PlatformAdmin
        using var platformAdminClient = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.Login, cancellationToken: cancellationToken);
        var platformAdminData = await GetMeAsync(platformAdminClient, cancellationToken);
        Assert.Equal(tenant.Id, platformAdminData.TenantId);
        Assert.Contains(SystemRole.PlatformAdmin.Name, platformAdminData.Roles);
        Assert.True(platformAdminData.IsAdmin);
    }

    private static async Task<AuthorizationData> GetMeAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var meResponse = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);
        meResponse.EnsureSuccessStatusCode();

        var mePayload = await meResponse.Content.ReadFromJsonAsync<AuthorizationData>(cancellationToken);
        Assert.NotNull(mePayload);
        return mePayload;
    }
}
