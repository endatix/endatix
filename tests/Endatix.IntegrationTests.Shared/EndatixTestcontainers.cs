using System.Data.Common;
using System.Diagnostics;
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
/// Shared Testcontainers session: one Docker network and one database container per test process, one template
/// database on it (see <see cref="IntegrationDatabaseTemplate"/>), and a database of its own for every fixture.
/// Ryuk removes resources when the test host exits unless <c>ENDATIX_TEST_REUSE_CONTAINERS</c> is enabled.
/// </summary>
internal static class EndatixTestcontainers
{
    private const string SqlServerPassword = "yourStrong(!)Password";

    /// <summary>
    /// Same default the obsolete parameterless <see cref="PostgreSqlBuilder"/> used
    /// (<c>PostgreSqlBuilder.PostgreSqlImage</c> = <c>postgres:15.1</c>).
    /// </summary>
    private const string DefaultPostgreSqlImage = "postgres:15.1";

    /// <summary>SQL Server 2025+ is required for native <c>json</c> columns used by AppDbContext migrations.</summary>
    private const string DefaultSqlServerImage = "mcr.microsoft.com/mssql/server:2025-latest";

    /// <summary>How old a fixture or template database on a reused container must be before a new run drops it.</summary>
    private static readonly TimeSpan LeftoverAge = TimeSpan.FromHours(12);

    // Also serializes the copies: PostgreSQL can refuse to copy a template while another copy of it is being made (55006).
    private static readonly SemaphoreSlim _sync = new(1, 1);

    private static INetwork? _network;
    private static IContainer? _databaseContainer;
    private static TestDatabaseProvider _provider;
    private static TestDatabaseServer? _server;
    private static Task<string?>? _template;
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

    /// <summary>
    /// Starts the provider's container on first use, builds the template database once, and creates a database of the
    /// caller's own from it.
    /// </summary>
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

            return await CreateFixtureDatabaseAsync(_server!, settings.ContainerSettings, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    private static async Task<EndatixTestcontainersSession> CreateFixtureDatabaseAsync(
        TestDatabaseServer server,
        EndatixTestcontainersSettings containerSettings,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var template = await EnsureTemplate(server, containerSettings).WaitAsync(cancellationToken);
        var templateWait = Stopwatch.GetElapsedTime(started);

        var name = TestDatabaseServer.NewName(TestDatabaseServer.FixturePrefix, DateTime.UtcNow);
        started = Stopwatch.GetTimestamp();
        try
        {
            await (template is null
                ? server.CreateEmptyAsync(name, cancellationToken)
                : server.CopyTemplateAsync(template, name, cancellationToken));
        }
        catch
        {
            // A restore can fail after it made the database; no fixture holds the name to drop it later.
            await DropBestEffortAsync(server, name, "after its failed copy");
            throw;
        }

        ReportSetup(
            $"{server.Provider} {name}: {(template is null ? "created empty" : "copied from template")} " +
            $"in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms (template wait {templateWait.TotalMilliseconds:F0} ms)");

        return new EndatixTestcontainersSession(
            name,
            server.ConnectionStringFor(name),
            server.ServerConnectionString,
            server.Provider,
            RunId,
            ResourceReaperSessionId);
    }

    /// <summary>Drops the caller's database; the container stays alive until the test process exits (Ryuk cleanup).</summary>
    public static Task ReleaseDatabaseAsync(EndatixTestcontainersSession session) =>
        new TestDatabaseServer(session.Provider, session.ServerConnectionString)
            .DropAsync(session.DatabaseName, CancellationToken.None);

    // Built once per process, on no caller's token: one caller's cancellation would otherwise fail every later fixture.
    // A failed build is not retried, so every later fixture fails with the same error.
    private static Task<string?> EnsureTemplate(
        TestDatabaseServer server,
        EndatixTestcontainersSettings containerSettings) =>
        _template ??= BuildTemplateAsync(server, containerSettings.ReuseContainers, CancellationToken.None);

    private static async Task<string?> BuildTemplateAsync(
        TestDatabaseServer server,
        bool reusedContainer,
        CancellationToken cancellationToken)
    {
        if (reusedContainer)
        {
            await DropLeftoversAsync(server, cancellationToken);
        }

        var migrate = IntegrationDatabaseTemplate.Seal();
        if (migrate is null)
        {
            return null;
        }

        var template = TestDatabaseServer.NewName(TestDatabaseServer.TemplatePrefix, DateTime.UtcNow);
        var started = Stopwatch.GetTimestamp();
        TimeSpan migrated;
        try
        {
            await server.CreateEmptyAsync(template, cancellationToken);
            await migrate(server.ConnectionStringFor(template), server.Provider, cancellationToken);
            migrated = Stopwatch.GetElapsedTime(started);
            started = Stopwatch.GetTimestamp();
            await server.SealTemplateAsync(template, cancellationToken);
        }
        catch
        {
            await DropBestEffortAsync(server, template, "after its failed build");
            throw;
        }

        ReportSetup(
            $"{server.Provider} {template}: created and migrated in {migrated.TotalMilliseconds:F0} ms, " +
            $"sealed in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        return template;
    }

    // Only databases old enough to belong to a run that has ended: another test process may share a reused container.
    // Best effort: a leftover another run drops first, or one that will not go, must not fail this run's fixtures.
    private static async Task DropLeftoversAsync(TestDatabaseServer server, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - LeftoverAge;
        List<string> leftovers;
        try
        {
            leftovers = await server.LeftoversAsync(cancellationToken);
        }
        catch (DbException ex)
        {
            ReportSetup($"{server.Provider}: could not list leftover databases: {ex.Message}");
            return;
        }

        foreach (var leftover in leftovers.Where(name => TestDatabaseServer.MadeBefore(name, cutoff)))
        {
            await DropBestEffortAsync(server, leftover, "left by an earlier run");
        }
    }

    // Reports a failed drop instead of throwing it: the caller either rethrows a failure of its own or must go on.
    private static async Task DropBestEffortAsync(TestDatabaseServer server, string name, string why)
    {
        try
        {
            await server.UnmarkTemplateAsync(name, CancellationToken.None);
            await server.DropAsync(name, CancellationToken.None);
        }
        catch (DbException ex)
        {
            ReportSetup($"{server.Provider}: could not drop {name} {why}: {ex.Message}");
        }
    }

    // A diagnostic message, shown when the runner's diagnosticMessages is on: the per-collection setup cost.
    private static void ReportSetup(string message) =>
        TestContext.Current.SendDiagnosticMessage($"[endatix-db] {message}");

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
        string serverConnectionString;
        (_databaseContainer, serverConnectionString) = settings.Provider switch
        {
            TestDatabaseProvider.PostgreSql => await StartPostgreSqlAsync(settings, cancellationToken),
            TestDatabaseProvider.SqlServer => await StartSqlServerAsync(settings, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.Provider,
                "Unsupported test database provider."),
        };
        _server = new TestDatabaseServer(settings.Provider, serverConnectionString);
    }

    private static async Task<(IContainer Container, string ConnectionString)> StartPostgreSqlAsync(
        IntegrationDatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        var containerSettings = settings.ContainerSettings;
        var postgresBuilder = new PostgreSqlBuilder(
                NormalizeImageOverride(settings.PostgreSqlImage) ?? DefaultPostgreSqlImage)
            .WithDatabase("endatix_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithNetwork(_network!)
            .WithNetworkAliases("postgres")
            .WithName(ContainerName(containerSettings, "postgres"))
            // Docker's default 64 MB /dev/shm fails parallel queries on large tables (53100).
            .WithCreateParameterModifier(parameters => (parameters.HostConfig ??= new()).ShmSize = 1L << 30);

        postgresBuilder = ApplyReuse(
            ApplySession(postgresBuilder, containerSettings, EndatixTestcontainerLabels.ComponentPostgres),
            containerSettings,
            "endatix-postgres");

        var postgresContainer = postgresBuilder.Build();
        await postgresContainer.StartAsync(cancellationToken);
        return (postgresContainer, postgresContainer.GetConnectionString());
    }

    // Testcontainers creates the database at startup, and its connection string names it rather than master. It stays
    // empty: it is the server connection that creates, copies and drops the fixture databases.
    private static async Task<(IContainer Container, string ConnectionString)> StartSqlServerAsync(
        IntegrationDatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        var containerSettings = settings.ContainerSettings;
        var sqlBuilder = new MsSqlBuilder(
                NormalizeImageOverride(settings.SqlServerImage) ?? DefaultSqlServerImage)
            .WithDatabase("endatix_test")
            .WithPassword(SqlServerPassword)
            .WithNetwork(_network!)
            .WithNetworkAliases("sqlserver")
            .WithName(ContainerName(containerSettings, "sqlserver"));

        sqlBuilder = ApplyReuse(
            ApplySession(sqlBuilder, containerSettings, EndatixTestcontainerLabels.ComponentSqlServer),
            containerSettings,
            "endatix-sqlserver");

        var sqlContainer = sqlBuilder.Build();
        await sqlContainer.StartAsync(cancellationToken);
        return (sqlContainer, sqlContainer.GetConnectionString());
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

/// <summary>A fixture's own database on the shared container, and the container's server connection.</summary>
internal sealed record EndatixTestcontainersSession(
    string DatabaseName,
    string ConnectionString,
    string ServerConnectionString,
    TestDatabaseProvider Provider,
    string RunId,
    Guid ResourceReaperSessionId);
