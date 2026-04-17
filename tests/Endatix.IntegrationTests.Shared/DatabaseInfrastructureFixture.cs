using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Shared database infrastructure fixture with provider selection via environment.
/// </summary>
public sealed class DatabaseInfrastructureFixture : IAsyncLifetime
{
    private const string SqlServerPassword = "yourStrong(!)Password";

    private IContainer _container = null!;

    public TestDatabaseProvider Provider { get; }

    public string ConnectionString { get; private set; } = string.Empty;

    public DatabaseCheckpoint Checkpoint { get; } = new();

    public DatabaseInfrastructureFixture()
        : this(IntegrationDatabaseSettings.FromEnvironment())
    {
    }

    public DatabaseInfrastructureFixture(IntegrationDatabaseSettings settings)
    {
        Provider = settings.Provider;

        switch (settings.Provider)
        {
            case TestDatabaseProvider.PostgreSql:
                var postgresBuilder = new PostgreSqlBuilder()
                    .WithDatabase("endatix_test")
                    .WithUsername("postgres")
                    .WithPassword("postgres");

                if (!string.IsNullOrWhiteSpace(settings.PostgreSqlImage))
                {
                    postgresBuilder = postgresBuilder.WithImage(settings.PostgreSqlImage);
                }

                var postgresContainer = postgresBuilder.Build();
                _container = postgresContainer;
                break;
            case TestDatabaseProvider.SqlServer:
                vader = new MsSqlBuilder()
                    .WithPassword(SqlServerPassword);

                if (!string.IsNullOrWhiteSpace(settings.SqlServerImage))
                {
                    sqlBuilder = sqlBuilder.WithImage(settings.SqlServerImage);
                }

                var sqlContainer = sqlBuilder.Build();
                _container = sqlContainer;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings.Provider), settings.Provider, "Unsupported test database provider.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = Provider switch
        {
            TestDatabaseProvider.PostgreSql => ((PostgreSqlContainer)_container).GetConnectionString(),
            TestDatabaseProvider.SqlServer => ((MsSqlContainer)_container).GetConnectionString(),
            _ => throw new ArgumentOutOfRangeException(nameof(Provider), Provider, "Unsupported test database provider.")
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
