using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Containerized database + <see cref="EndatixWebApplicationFactory" /> for one xUnit collection.
/// Database provider comes from <c>ENDATIX_TEST_DB_PROVIDER</c> (default PostgreSQL).
/// </summary>
public sealed class EndatixIntegrationWebHostFixture : IAsyncLifetime, IIntegrationTestHostFixture
{
    private IEndatixWebApplicationFactory? _factory;

    public DatabaseInfrastructureFixture Database { get; } = new();

    public IEndatixWebApplicationFactory Factory =>
        _factory ?? throw new InvalidOperationException("The fixture is not initialized, so it has no host.");

    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    /// <summary>
    /// Active test database provider (<c>ENDATIX_TEST_DB_PROVIDER</c>).
    /// </summary>
    public TestDatabaseProvider Provider => Database.Provider;

    IServiceProvider IIntegrationTestHostFixture.Services => Factory.Services;

    public IntegrationSeedBuilder Seed { get; private set; } = null!;

    /// <summary>The shared host's outbox relay and jobs scheduler, off until a test starts them.</summary>
    internal BackgroundWork BackgroundWork => BackgroundWork.Of(Factory.Services);

    public HttpClient CreateClient() => Factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        _factory = new EndatixWebApplicationFactory(Database.ConnectionString, Database.Provider);
        Seed = new IntegrationSeedBuilder(Factory.Services);

        await IntegrationHostWarmup.EnsureReadyAsync(Factory.Services, Factory.CreateClient);
    }

    // xUnit disposes a collection fixture whose InitializeAsync failed too, so the factory may never have been created;
    // its database session is released either way.
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }
        }
        finally
        {
            await Database.DisposeAsync();
        }
    }
}
