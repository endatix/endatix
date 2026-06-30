namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Shared database infrastructure fixture with provider selection via environment.
/// All fixtures in the same test process share one container via <see cref="EndatixTestcontainers" />.
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
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

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
    /// Starts the database container (shared per process) and captures connection details.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _session = await EndatixTestcontainers.AcquireDatabaseAsync(_settings);
        ConnectionString = _session.ConnectionString;
        Provider = _session.Provider;
        RunId = _session.RunId;
    }

    /// <summary>
    /// Releases the session reference; the container stays alive until Ryuk cleans up.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await EndatixTestcontainers.ReleaseSessionAsync();
        _session = null;
    }
}
