using DotNet.Testcontainers.Images;
using Testcontainers.Keycloak;

namespace Endatix.IntegrationTests.Shared.KeycloakInfra;

/// <summary>
/// Keycloak testcontainer Joins the shared Endatix test Docker network when present.
/// </summary>
public sealed class KeycloakTestContainerFixture : IAsyncLifetime
{
    private readonly EndatixTestcontainersSettings _settings = EndatixTestcontainersSettings.FromEnvironment();

    private KeycloakContainer? _container;

    /// <summary>
    /// The Keycloak testcontainer.
    /// </summary>
    public KeycloakContainer Container =>
        _container ?? throw new InvalidOperationException("Keycloak container was not initialized.");

    /// <summary>
    /// Initializes the Keycloak testcontainer.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        try
        {
            var network =
                await EndatixTestcontainers.AcquireNetworkAsync(_settings);

            var builder = new KeycloakBuilder(new DockerImage("quay.io/keycloak/keycloak:26.6"))
                .WithNetwork(network)
                .WithNetworkAliases("keycloak");

            builder = EndatixTestcontainers.ConfigureKeycloakBuilder(builder, _settings);

            _container = builder.Build();
            await _container.StartAsync();
        }
        catch
        {
            await EndatixTestcontainers.ReleaseSessionAsync();
            throw;
        }
    }

    /// <summary>
    /// Gets the base URI of the running Keycloak container.
    /// </summary>
    public Uri GetBaseUri() => new(Container.GetBaseAddress());

    /// <summary>
    /// Disposes the Keycloak testcontainer.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
        finally
        {
            await EndatixTestcontainers.ReleaseSessionAsync();
        }
    }
}
