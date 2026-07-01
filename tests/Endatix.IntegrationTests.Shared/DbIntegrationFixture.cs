namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Database-only fixture for integration tests that need a real DB without <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}" />.
/// </summary>
public sealed class DbIntegrationFixture : IAsyncLifetime
{
    /// <summary>
    /// The database infrastructure fixture.
    /// </summary>
    public DatabaseInfrastructureFixture Database { get; } = new();

    /// <summary>
    /// Convenience accessor to the underlying database checkpoint.
    /// </summary>
    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    /// <summary>
    /// Convenience accessor to the underlying connection string.
    /// </summary>
    public string ConnectionString => Database.ConnectionString;

    /// <summary>
    /// Convenience accessor to the underlying database provider.
    /// </summary>
    public TestDatabaseProvider Provider => Database.Provider;

    /// <summary>
    /// Initializes the database infrastructure (starts container on first call).
    /// </summary>
    public async ValueTask InitializeAsync() => await Database.InitializeAsync();

    /// <summary>
    /// Disposes the database infrastructure.
    /// </summary>
    public async ValueTask DisposeAsync() => await Database.DisposeAsync();
}
