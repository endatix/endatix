namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Contract for integration test host fixtures that provide database, services, and HTTP client access.
/// </summary>
public interface IIntegrationTestHostFixture
{
    /// <summary>
    /// Database infrastructure (container, connection string, checkpoint).
    /// </summary>
    DatabaseInfrastructureFixture Database { get; }

    /// <summary>
    /// Convenience accessor for the database checkpoint.
    /// </summary>
    DatabaseCheckpoint Checkpoint { get; }

    /// <summary>
    /// Root service provider from the test host.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Seed builder for per-test data setup.
    /// </summary>
    IntegrationSeedBuilder Seed { get; }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pointed at the test host.
    /// </summary>
    HttpClient CreateClient();
}
