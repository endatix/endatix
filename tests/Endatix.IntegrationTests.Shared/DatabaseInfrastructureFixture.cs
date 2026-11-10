namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// A database of this fixture's own, with provider selection via environment. All fixtures in the same test process
/// share one container via <see cref="EndatixTestcontainers" />, and each gets a copy of the template database
/// (<see cref="IntegrationDatabaseTemplate"/>), dropped when the fixture is disposed.
/// </summary>
/// <remarks>
/// Initializes the database infrastructure fixture.
/// </remarks>
public sealed class DatabaseInfrastructureFixture(IntegrationDatabaseSettings settings) : IAsyncLifetime
{
    private readonly IntegrationDatabaseSettings _settings = settings;
    private EndatixTestcontainersSession? _session;

    /// <summary>
    /// The database provider.
    /// </summary>
    public TestDatabaseProvider Provider { get; private set; }

    /// <summary>
    /// The connection string of this fixture's own database. No other fixture uses it.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// A connection string to the database server rather than to this fixture's database, for a test that creates
    /// databases of its own. Its database is the container's maintenance database, which stays empty.
    /// </summary>
    public string ServerConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Checkpoint for resetting the database between tests.
    /// </summary>
    public DatabaseCheckpoint Checkpoint { get; } = new();

    /// <summary>Correlation id for Docker labels on this test run (see tests README for docker filter examples).</summary>
    public string RunId { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes the database infrastructure fixture.
    /// </summary>
    public DatabaseInfrastructureFixture()
        : this(IntegrationDatabaseSettings.FromEnvironment())
    {
    }

    /// <summary>
    /// Starts the database container (shared per process) and creates this fixture's database on it.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _session = await EndatixTestcontainers.AcquireDatabaseAsync(_settings);
        ConnectionString = _session.ConnectionString;
        ServerConnectionString = _session.ServerConnectionString;
        Provider = _session.Provider;
        RunId = _session.RunId;
    }

    /// <summary>
    /// Drops this fixture's database; the container stays alive until Ryuk cleans up. Dispose a host on it first.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_session is null)
        {
            return;
        }

        var session = _session;
        _session = null;
        await EndatixTestcontainers.ReleaseDatabaseAsync(session);
    }
}
