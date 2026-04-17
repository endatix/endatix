using Endatix.IntegrationTests.Shared;
using Xunit;

namespace Endatix.IntegrationTests;

/// <summary>
/// Containerized database infrastructure + <see cref="OssWebApplicationFactory" /> for one xUnit collection.
/// </summary>
public sealed class OssIntegrationWebHostFixture : IAsyncLifetime
{
    private readonly DatabaseInfrastructureFixture _database = new();
    private readonly IntegrationHostSettings _hostSettings = IntegrationHostSettings.FromEnvironment();

    public DatabaseInfrastructureFixture Database => _database;

    public IOssWebApplicationFactory Factory { get; private set; } = null!;
    public IntegrationSeedBuilder Seed { get; private set; } = null!;

    public DatabaseCheckpoint Checkpoint => _database.Checkpoint;

    public async Task<StandardSeedResult?> ResetDatabaseAsync(
        bool useStandardSeed = false,
        StandardSeedOptions? options = null,
        Func<IServiceProvider, StandardSeedResult, CancellationToken, Task>? afterSeed = null,
        CancellationToken cancellationToken = default)
    {
        await Checkpoint.ResetAsync(
            Database.ConnectionString,
            Database.Provider,
            cancellationToken);

        if (!useStandardSeed)
        {
            return null;
        }

        return await Seed.SeedStandardAsync(options, afterSeed, cancellationToken);
    }

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        Factory = _hostSettings.HostMode switch
        {
            IntegrationHostMode.ProductionProgram => new OssWebApplicationFactory(_database.ConnectionString, _database.Provider),
            IntegrationHostMode.DedicatedIntegrationHost => new OssDedicatedHostWebApplicationFactory(_database.ConnectionString, _database.Provider),
            _ => throw new ArgumentOutOfRangeException(nameof(_hostSettings.HostMode), _hostSettings.HostMode, "Unsupported integration host mode.")
        };
        Seed = new IntegrationSeedBuilder(Factory.Services);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
