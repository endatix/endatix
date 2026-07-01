using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.Keycloak;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Shared Testcontainers session: one Docker network and one database container per test process.
/// Ryuk removes resources when the test host exits unless <c>ENDATIX_TEST_REUSE_CONTAINERS</c> is enabled.
/// </summary>
internal static class EndatixTestcontainers
{
    private const string SqlServerPassword = "yourStrong(!)Password";

    /// <summary>SQL Server 2025+ is required for native <c>json</c> columns used by AppDbContext migrations.</summary>
    private const string DefaultSqlServerImage = "mcr.microsoft.com/mssql/server:2025-latest";

    private static readonly SemaphoreSlim _sync = new(1, 1);

    private static INetwork? _network;
    private static IContainer? _databaseContainer;
    private static TestDatabaseProvider _provider;
    private static string _connectionString = string.Empty;
    private static string? _runId;
    private static bool _databaseStarted;
    private static IntegrationDatabaseSettings? _activeDatabaseSettings;

    public static string RunId =>
        _runId ??= Environment.GetEnvironmentVariable("ENDATIX_TEST_RUN_ID")
            ?? Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Ryuk session id for the current test process. All session containers share this value.
    /// </summary>
    public static Guid ResourceReaperSessionId { get; private set; }

    public static async Task<EndatixTestcontainersSession> AcquireDatabaseAsync(
        IntegrationDatabaseSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!_databaseStarted)
            {
                await EnsureSessionStartedAsync(settings.ContainerSettings, cancellationToken);
                await StartDatabaseContainerAsync(settings, cancellationToken);
                _databaseStarted = true;
                _activeDatabaseSettings = settings;
            }
            else
            {
                EnsureActiveDatabaseSettingsMatch(settings);
            }

            return new EndatixTestcontainersSession(
                _connectionString,
                _provider,
                RunId,
                ResourceReaperSessionId);
        }
        finally
        {
            _sync.Release();
        }
    }

    public static async Task<INetwork> AcquireNetworkAsync(
        EndatixTestcontainersSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            await EnsureSessionStartedAsync(settings, cancellationToken);
            return _network!;
        }
        finally
        {
            _sync.Release();
        }
    }

    internal static KeycloakBuilder ConfigureKeycloakBuilder(
        KeycloakBuilder builder,
        EndatixTestcontainersSettings settings)
    {
        builder = ApplySession(builder, settings, EndatixTestcontainerLabels.ComponentKeycloak);
        builder = builder.WithName(ContainerName(settings, "keycloak"));
        return ApplyReuse(builder, settings, "endatix-keycloak");
    }

    /// <summary>
    /// Fixtures call this on dispose; the shared session stays alive until the test process exits (Ryuk cleanup).
    /// </summary>
    public static Task ReleaseSessionAsync(CancellationToken _ = default) =>
        Task.CompletedTask;

    private static async Task EnsureSessionStartedAsync(
        EndatixTestcontainersSettings settings,
        CancellationToken cancellationToken)
    {
        if (_network is not null)
        {
            return;
        }

        await EnsureResourceReaperAsync(cancellationToken);

        var projectName = ComposeProjectName(settings);

        var networkBuilder = ApplyReuse(
            ApplySession(
                new NetworkBuilder().WithName(projectName),
                settings,
                EndatixTestcontainerLabels.ComponentNetwork),
            settings,
            "endatix-network");

        _network = networkBuilder.Build();
        await _network.CreateAsync(cancellationToken);
    }

    private static async Task EnsureResourceReaperAsync(CancellationToken cancellationToken)
    {
        if (ResourceReaperSessionId != Guid.Empty)
        {
            return;
        }

        var resourceReaper = await ResourceReaper.GetAndStartDefaultAsync(
            TestcontainersSettings.OS.DockerEndpointAuthConfig,
            NullLogger.Instance,
            false,
            cancellationToken).ConfigureAwait(false);

        ResourceReaperSessionId = resourceReaper.SessionId;
    }

    private static async Task StartDatabaseContainerAsync(
        IntegrationDatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        _provider = settings.Provider;
        var containerSettings = settings.ContainerSettings;

        switch (settings.Provider)
        {
            case TestDatabaseProvider.PostgreSql:
            {
                var postgresBuilder = new PostgreSqlBuilder()
                    .WithDatabase("endatix_test")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .WithNetwork(_network!)
                    .WithNetworkAliases("postgres")
                    .WithName(ContainerName(containerSettings, "postgres"));

                if (!string.IsNullOrWhiteSpace(settings.PostgreSqlImage))
                {
                    postgresBuilder = postgresBuilder.WithImage(settings.PostgreSqlImage);
                }

                postgresBuilder = ApplyReuse(
                    ApplySession(postgresBuilder, containerSettings, EndatixTestcontainerLabels.ComponentPostgres),
                    containerSettings,
                    "endatix-postgres");

                var postgresContainer = postgresBuilder.Build();
                _databaseContainer = postgresContainer;
                await _databaseContainer.StartAsync(cancellationToken);
                _connectionString = postgresContainer.GetConnectionString();
                break;
            }
            case TestDatabaseProvider.SqlServer:
            {
                var sqlBuilder = new MsSqlBuilder()
                    .WithPassword(SqlServerPassword)
                    .WithImage(settings.SqlServerImage ?? DefaultSqlServerImage)
                    .WithNetwork(_network!)
                    .WithNetworkAliases("sqlserver")
                    .WithName(ContainerName(containerSettings, "sqlserver"));

                sqlBuilder = ApplyReuse(
                    ApplySession(sqlBuilder, containerSettings, EndatixTestcontainerLabels.ComponentSqlServer),
                    containerSettings,
                    "endatix-sqlserver");

                var sqlContainer = sqlBuilder.Build();
                _databaseContainer = sqlContainer;
                await _databaseContainer.StartAsync(cancellationToken);
                _connectionString = sqlContainer.GetConnectionString();
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    settings.Provider,
                    "Unsupported test database provider.");
        }
    }

    private static void EnsureActiveDatabaseSettingsMatch(IntegrationDatabaseSettings requested)
    {
        var active = _activeDatabaseSettings
            ?? throw new InvalidOperationException(
                $"{nameof(AcquireDatabaseAsync)} session is started but active database settings were not recorded.");

        if (requested.Provider != _provider || requested.Provider != active.Provider)
        {
            throw new InvalidOperationException(
                $"Cannot acquire {requested.Provider} database: this test process already started {_provider} via {nameof(AcquireDatabaseAsync)}.");
        }

        if (!ImageOverridesMatch(requested.PostgreSqlImage, active.PostgreSqlImage))
        {
            throw new InvalidOperationException(
                $"PostgreSQL image override conflict: requested '{FormatImageOverride(requested.PostgreSqlImage)}' but active session uses '{FormatImageOverride(active.PostgreSqlImage)}'.");
        }

        if (!ImageOverridesMatch(requested.SqlServerImage, active.SqlServerImage))
        {
            throw new InvalidOperationException(
                $"SQL Server image override conflict: requested '{FormatImageOverride(requested.SqlServerImage)}' but active session uses '{FormatImageOverride(active.SqlServerImage)}'.");
        }

        if (requested.ContainerSettings != active.ContainerSettings)
        {
            throw new InvalidOperationException(
                $"Container settings conflict: requested ReuseContainers={requested.ContainerSettings.ReuseContainers} but active session uses ReuseContainers={active.ContainerSettings.ReuseContainers}.");
        }
    }

    private static bool ImageOverridesMatch(string? requested, string? active) =>
        string.Equals(
            NormalizeImageOverride(requested),
            NormalizeImageOverride(active),
            StringComparison.Ordinal);

    private static string? NormalizeImageOverride(string? image) =>
        string.IsNullOrWhiteSpace(image) ? null : image.Trim();

    private static string FormatImageOverride(string? image) =>
        NormalizeImageOverride(image) ?? "(default)";

    private static string ComposeProjectName(EndatixTestcontainersSettings settings) =>
        settings.ReuseContainers ? "endatix-tests" : $"endatix-tests-{RunId}";

    private static string ContainerName(EndatixTestcontainersSettings settings, string component) =>
        $"{ComposeProjectName(settings)}-{component}";

    private static NetworkBuilder ApplySession(
        NetworkBuilder builder,
        EndatixTestcontainersSettings settings,
        string component) =>
        builder
            .WithLabel(EndatixTestcontainerLabels.Suite, EndatixTestcontainerLabels.SuiteValue)
            .WithLabel(EndatixTestcontainerLabels.Component, component)
            .WithLabel(EndatixTestcontainerLabels.Run, RunId)
            .WithLabel(EndatixTestcontainerLabels.DockerComposeProject, ComposeProjectName(settings));

    private static PostgreSqlBuilder ApplySession(
        PostgreSqlBuilder builder,
        EndatixTestcontainersSettings settings,
        string component) =>
        builder
            .WithLabel(EndatixTestcontainerLabels.Suite, EndatixTestcontainerLabels.SuiteValue)
            .WithLabel(EndatixTestcontainerLabels.Component, component)
            .WithLabel(EndatixTestcontainerLabels.Run, RunId)
            .WithLabel(EndatixTestcontainerLabels.DockerComposeProject, ComposeProjectName(settings));

    private static MsSqlBuilder ApplySession(
        MsSqlBuilder builder,
        EndatixTestcontainersSettings settings,
        string component) =>
        builder
            .WithLabel(EndatixTestcontainerLabels.Suite, EndatixTestcontainerLabels.SuiteValue)
            .WithLabel(EndatixTestcontainerLabels.Component, component)
            .WithLabel(EndatixTestcontainerLabels.Run, RunId)
            .WithLabel(EndatixTestcontainerLabels.DockerComposeProject, ComposeProjectName(settings));

    private static KeycloakBuilder ApplySession(
        KeycloakBuilder builder,
        EndatixTestcontainersSettings settings,
        string component) =>
        builder
            .WithLabel(EndatixTestcontainerLabels.Suite, EndatixTestcontainerLabels.SuiteValue)
            .WithLabel(EndatixTestcontainerLabels.Component, component)
            .WithLabel(EndatixTestcontainerLabels.Run, RunId)
            .WithLabel(EndatixTestcontainerLabels.DockerComposeProject, ComposeProjectName(settings));

    private static NetworkBuilder ApplyReuse(
        NetworkBuilder builder,
        EndatixTestcontainersSettings settings,
        string reuseId) =>
        settings.ReuseContainers
            ? builder.WithReuse(true).WithLabel("reuse-id", reuseId)
            : builder;

    private static PostgreSqlBuilder ApplyReuse(
        PostgreSqlBuilder builder,
        EndatixTestcontainersSettings settings,
        string reuseId) =>
        settings.ReuseContainers
            ? builder.WithReuse(true).WithLabel("reuse-id", reuseId)
            : builder;

    private static MsSqlBuilder ApplyReuse(
        MsSqlBuilder builder,
        EndatixTestcontainersSettings settings,
        string reuseId) =>
        settings.ReuseContainers
            ? builder.WithReuse(true).WithLabel("reuse-id", reuseId)
            : builder;

    private static KeycloakBuilder ApplyReuse(
        KeycloakBuilder builder,
        EndatixTestcontainersSettings settings,
        string reuseId) =>
        settings.ReuseContainers
            ? builder.WithReuse(true).WithLabel("reuse-id", reuseId)
            : builder;
}

internal sealed record EndatixTestcontainersSession(
    string ConnectionString,
    TestDatabaseProvider Provider,
    string RunId,
    Guid ResourceReaperSessionId);
