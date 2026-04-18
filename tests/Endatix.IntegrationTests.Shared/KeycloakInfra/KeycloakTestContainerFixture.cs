using Testcontainers.Keycloak;
using Xunit;

namespace Endatix.IntegrationTests.Shared.KeycloakInfra;

/// <summary>
/// Keycloak testcontainer (Phase 2 RBAC). Uses the default dev image and admin credentials.
/// </summary>
public sealed class KeycloakTestContainerFixture : IAsyncLifetime
{
    public KeycloakContainer Container { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = new KeycloakBuilder()
            .WithImage("quay.io/keycloak/keycloak:26.0");
        Container = builder.Build();
        await Container.StartAsync();
    }

    public Uri GetBaseUri() => new(Container.GetBaseAddress());

    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
