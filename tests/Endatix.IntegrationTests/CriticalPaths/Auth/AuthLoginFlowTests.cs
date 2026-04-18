using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Endatix.Api.Endpoints.Auth;
using Endatix.Core.Abstractions.Authorization;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

[Collection(nameof(OssIntegrationTestCollection))]
[Trait("Category", "CriticalPath")]
[Trait("Priority", "P0")]
public sealed class AuthLoginFlowTests
{
    private const string SeedPassword = "Password123!";
    private readonly OssIntegrationWebHostFixture _fixture;

    public AuthLoginFlowTests(OssIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_and_me_return_expected_roles_and_permissions_for_seeded_role_users()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = StandardSeedOptions.CreateDefault() with
        {
            DefaultPassword = SeedPassword
        };

        var seedResult = await _fixture.ResetDatabaseAsync(
            useStandardSeed: true,
            options: options,
            cancellationToken: cancellationToken);

        Assert.NotNull(seedResult);
        Assert.NotEmpty(seedResult.TenantIds);
        var firstTenantId = seedResult.TenantIds[0];
        var tenant = options.Tenants[0];

        using var adminClient = _fixture.Factory.CreateClient();
        var adminData = await LoginAndGetMeAsync(adminClient, tenant.AdminEmail, "admin", cancellationToken);
        Assert.Equal(firstTenantId, adminData.TenantId);
        Assert.Contains(SystemRole.Authenticated.Name, adminData.Roles);
        Assert.Contains(SystemRole.Admin.Name, adminData.Roles);
        Assert.Contains(Actions.Access.Authenticated, adminData.Permissions);
        Assert.Contains(Actions.Access.Hub, adminData.Permissions);
        Assert.True(adminData.IsAdmin);

        using var creatorClient = _fixture.Factory.CreateClient();
        var creatorData = await LoginAndGetMeAsync(creatorClient, tenant.CreatorEmail, "creator", cancellationToken);
        Assert.Equal(firstTenantId, creatorData.TenantId);
        Assert.Contains(SystemRole.Authenticated.Name, creatorData.Roles);
        Assert.Contains(SystemRole.Creator.Name, creatorData.Roles);
        Assert.Contains(Actions.Access.Authenticated, creatorData.Permissions);
        Assert.Contains(Actions.Forms.Create, creatorData.Permissions);
        Assert.Contains(Actions.Submissions.CreateOnBehalf, creatorData.Permissions);
        Assert.False(creatorData.IsAdmin);

        using var platformAdminClient = _fixture.Factory.CreateClient();
        var platformAdminData = await LoginAndGetMeAsync(platformAdminClient, tenant.PlatformAdminEmail, "platform-admin", cancellationToken);
        Assert.Equal(firstTenantId, platformAdminData.TenantId);
        Assert.Contains(SystemRole.Authenticated.Name, platformAdminData.Roles);
        Assert.Contains(SystemRole.PlatformAdmin.Name, platformAdminData.Roles);
        Assert.Contains(Actions.Access.Authenticated, platformAdminData.Permissions);
        Assert.True(platformAdminData.IsAdmin);
    }

    private static async Task<AuthorizationData> LoginAndGetMeAsync(
        HttpClient client,
        string email,
        string scenario,
        CancellationToken cancellationToken)
    {
        LoginRequest loginRequest = new(email, SeedPassword);
        var loginResponse = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            loginRequest,
            cancellationToken);
        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        Assert.NotNull(loginPayload);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload.AccessToken);
        var meResponse = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), cancellationToken);
        meResponse.EnsureSuccessStatusCode();

        var mePayload = await meResponse.Content.ReadFromJsonAsync<AuthorizationData>(cancellationToken);
        Assert.NotNull(mePayload);
        if (IsDebugLoggingEnabled())
        {
            Console.WriteLine($"[debug] scenario={scenario}, email={email}");
            Console.WriteLine(JsonSerializer.Serialize(mePayload, new JsonSerializerOptions { WriteIndented = true }));
        }

        return mePayload;
    }

    private static bool IsDebugLoggingEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("ENDATIX_TEST_DEBUG"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }
}
