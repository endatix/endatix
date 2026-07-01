using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Containerized database + <see cref="EndatixWebApplicationFactory" /> for one xUnit collection.
/// Database provider comes from <c>ENDATIX_TEST_DB_PROVIDER</c> (default PostgreSQL).
/// </summary>
public sealed class EndatixIntegrationWebHostFixture : IAsyncLifetime, IIntegrationTestHostFixture
{
    private readonly IntegrationHostSettings _hostSettings = IntegrationHostSettings.FromEnvironment();

    public DatabaseInfrastructureFixture Database { get; } = new();

    public IEndatixWebApplicationFactory Factory { get; private set; } = null!;

    public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

    IServiceProvider IIntegrationTestHostFixture.Services => Factory.Services;

    public IntegrationSeedBuilder Seed { get; private set; } = null!;

    public HttpClient CreateClient() => Factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();
        Factory = _hostSettings.HostMode switch
        {
            IntegrationHostMode.ProductionProgram => new EndatixWebApplicationFactory(Database.ConnectionString, Database.Provider),
            IntegrationHostMode.DedicatedIntegrationHost => new EndatixDedicatedHostWebApplicationFactory(Database.ConnectionString, Database.Provider),
            _ => throw new ArgumentOutOfRangeException(nameof(_hostSettings.HostMode), _hostSettings.HostMode, "Unsupported integration host mode.")
        };
        Seed = new IntegrationSeedBuilder(Factory.Services);

        await IntegrationHostWarmup.EnsureReadyAsync(Factory.Services, Factory.CreateClient);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
