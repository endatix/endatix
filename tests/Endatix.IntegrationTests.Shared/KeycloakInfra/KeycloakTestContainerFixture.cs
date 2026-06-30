using Testcontainers.Keycloak;
using Xunit;

namespace Endatix.IntegrationTests.Shared.KeycloakInfra;

/// <summary>
/// Keycloak testcontainer Joins the shared Endatix test Docker network when present.
/// </summary>
public sealed class KeycloakTestContainerFixture : IAsyncLifetime
{
    private readonly EndatixTestcontainersSettings _settings = EndatixTestcontainersSettings.FromEnvironment();

    /// <summary>
    /// The Keycloak testcontainer.
    /// </summary>
    public KeycloakContainer Container { get; private set; } = null!;

    /// <summary>
    /// Initializes the Keycloak testcontainer.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        var network =
            await EndatixTestcontainers.AcquireNetworkAsync(_settings);

        var builder = new KeycloakBuilder()
            .WithImage("quay.io/keycloak/keycloak:26.0")
            .WithNetwork(network)
            .WithNetworkAliases("keycloak");

        builder = EndatixTestcontainers.ConfigureKeycloakBuilder(builder, _settings);

        Container = builder.Build();
        await Container.StartAsync();
    }

    public Uri GetBaseUri() => new(Container.GetBaseAddress());

    /// <summary>
    /// Disposes the Keycloak testcontainer.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Container.DisposeAsync();
        await EndatixTestcontainers.ReleaseSessionAsync();
    }
}
