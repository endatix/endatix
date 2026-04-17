using Endatix.IntegrationTests.Shared.KeycloakInfra;

namespace Endatix.IntegrationTests;

/// <summary>
/// Keycloak container + password grant (Phase 2). Excluded from default CI via trait filter.
/// </summary>
[Trait("Category", "Keycloak")]
[Trait("Priority", "P2")]
public sealed class KeycloakRealmSmokeTests : IClassFixture<KeycloakTestContainerFixture>
{
    private readonly KeycloakTestContainerFixture _fixture;

    public KeycloakRealmSmokeTests(KeycloakTestContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Password_grant_returns_access_token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var baseUri = _fixture.GetBaseUri();
        var token = await KeycloakPasswordGrantTokenClient.GetAccessTokenAsync(
            baseUri,
            realm: "master",
            clientId: "admin-cli",
            username: "admin",
            password: "admin",
            cancellationToken);
        Assert.NotEmpty(token);
    }
}
