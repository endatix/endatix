using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// A web host on a database of its own, for a test that changes the schema, such as rolling a migration back, which
/// must not reach the collection's database. Its background work stays off.
/// </summary>
internal sealed class OwnDatabaseWebHost : IIntegrationTestHostFixture, IAsyncDisposable
{
    private readonly EndatixWebApplicationFactory _factory;

    private OwnDatabaseWebHost(DatabaseInfrastructureFixture database, EndatixWebApplicationFactory factory)
    {
        Database = database;
        _factory = factory;
        Seed = new IntegrationSeedBuilder(factory.Services);
    }

    public DatabaseInfrastructureFixture Database { get; }

    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    public IServiceProvider Services => _factory.Services;

    public IntegrationSeedBuilder Seed { get; }

    /// <summary>Creates a database from the template and starts a host on it.</summary>
    public static async Task<OwnDatabaseWebHost> StartAsync()
    {
        var database = new DatabaseInfrastructureFixture();
        await database.InitializeAsync();
        var factory = new EndatixWebApplicationFactory(database.ConnectionString, database.Provider);

        // Reading the services starts the host; a start that fails disposes the factory and drops the database.
        return await FailedHostStart.DisposeOnFailureAsync(
            () => Task.FromResult(new OwnDatabaseWebHost(database, factory)),
            factory.DisposeAsync,
            database.DisposeAsync);
    }

    public HttpClient CreateClient() => _factory.CreateClient();

    public ValueTask DisposeAsync() => DisposalFailures.DisposeHostThenDatabaseAsync(_factory, Database);
}
