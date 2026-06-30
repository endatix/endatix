namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// A prepared integration test world with access to services, seed data, and authenticated clients.
/// </summary>
public sealed class IntegrationTestWorld
{
    /// <summary>
    /// Root service provider from the test host.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Seed builder for additional per-test data.
    /// </summary>
    public required IntegrationSeedBuilder Seed { get; init; }

    /// <summary>
    /// The result of the standard seed run, or <c>null</c> if seeding was skipped.
    /// </summary>
    public required StandardSeedResult? SeedResult { get; init; }

    /// <summary>
    /// Factory for creating <see cref="HttpClient"/> instances pointed at the test host.
    /// </summary>
    public required Func<HttpClient> CreateClientFactory { get; init; }

    /// <summary>
    /// The options used to prepare this world.
    /// </summary>
    public required IntegrationWorldOptions Options { get; init; }

    /// <summary>
    /// Seeded tenants from <see cref="SeedResult"/>, or an empty list if no seed was run.
    /// </summary>
    public IReadOnlyList<SeededTenant> Tenants => SeedResult?.Tenants ?? [];

    /// <summary>
    /// Creates an anonymous HTTP client.
    /// </summary>
    public HttpClient AnonymousClient() => CreateClientFactory();

    /// <summary>
    /// Creates an HTTP client authenticated as the given <paramref name="persona"/>.
    /// </summary>
    public Task<HttpClient> AsAsync(
        TestPersona persona,
        int tenantIndex = 0,
        IntegrationAuthMode mode = IntegrationAuthMode.Login,
        string? userName = null,
        CancellationToken cancellationToken = default) =>
        IntegrationAuthClients.CreateClientAsync(this, persona, tenantIndex, mode, userName, cancellationToken);
}
