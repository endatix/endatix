using System.Data.Common;
using Endatix.IntegrationTests.Shared;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every fixture runs on a user database of its own, copied from the migrated template, never on one of the server's
/// system databases nor on another fixture's.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class TestDatabaseTests(DbIntegrationFixture fixture)
{
    private const string FixturePrefix = "endatix_fx_";

    [Fact]
    public async Task Fixture_connects_to_a_database_of_its_own()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var database = await On(fixture.Database).ScalarAsync(CurrentDatabaseSql(fixture.Provider), ct);
        var server = await Server(fixture).ScalarAsync(CurrentDatabaseSql(fixture.Provider), ct);

        // Assert
        database.Should().BeOfType<string>().Which.Should().StartWith(FixturePrefix);
        server.Should().Be("endatix_test");
    }

    [Fact]
    public async Task Two_fixtures_get_different_databases_and_do_not_see_each_others_rows()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var first = await StartedAsync();
        await using var second = await StartedAsync();
        await On(first).ExecuteAsync("""CREATE TABLE "IsolationProbe" ("Id" int)""", ct);
        await On(first).ExecuteAsync("""INSERT INTO "IsolationProbe" ("Id") VALUES (1)""", ct);

        // Act
        var firstName = await On(first).ScalarAsync(CurrentDatabaseSql(first.Provider), ct);
        var secondName = await On(second).ScalarAsync(CurrentDatabaseSql(second.Provider), ct);
        var probeInSecond = await On(second).ScalarAsync(TableCountSql(second.Provider, "IsolationProbe"), ct);

        // Assert
        firstName.Should().NotBe(secondName);
        Convert.ToInt32(probeInSecond).Should().Be(0);
    }

    [Fact]
    public async Task A_fixture_database_starts_with_every_module_migrated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await StartedAsync();

        // Act
        var forms = await IntegrationDbAssert.TableExistsAsync(
            database.ConnectionString, database.Provider, DefaultSchema(database.Provider), "Forms", ct);
        var reporting = await On(database).ScalarAsync(SchemaTableCountSql("reporting"), ct);
        var audience = await On(database).ScalarAsync(SchemaTableCountSql("audience"), ct);
        var jobs = await On(database).ScalarAsync(SchemaTableCountSql("jobs"), ct);

        // Assert
        forms.Should().BeTrue();
        Convert.ToInt32(reporting).Should().BePositive();
        Convert.ToInt32(audience).Should().BePositive();
        Convert.ToInt32(jobs).Should().BePositive();
    }

    [Fact]
    public async Task A_fixture_database_starts_with_no_scheduler_check_in()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await StartedAsync();

        // Act
        var checkIns = await On(database).ScalarAsync("SELECT COUNT(*) FROM jobs.qrtz_scheduler_state", ct);

        // Assert: the host that built the template never started its scheduler, so no node of it is in the cluster
        Convert.ToInt32(checkIns).Should().Be(0);
    }

    [Fact]
    public async Task A_disposed_fixture_drops_its_database()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var database = await StartedAsync();
        var name = (string)(await On(database).ScalarAsync(CurrentDatabaseSql(database.Provider), ct))!;

        // Act
        await database.DisposeAsync();

        // Assert
        var left = await Server(fixture).ScalarAsync(DatabaseCountSql(fixture.Provider, name), ct);
        Convert.ToInt32(left).Should().Be(0);
    }

    private static async Task<DatabaseInfrastructureFixture> StartedAsync()
    {
        var database = new DatabaseInfrastructureFixture();
        await database.InitializeAsync();
        return database;
    }

    private static string CurrentDatabaseSql(TestDatabaseProvider provider) => provider switch
    {
        TestDatabaseProvider.PostgreSql => "SELECT current_database()",
        TestDatabaseProvider.SqlServer => "SELECT DB_NAME()",
        _ => throw Unsupported(provider),
    };

    private static string DefaultSchema(TestDatabaseProvider provider) =>
        provider == TestDatabaseProvider.PostgreSql ? "public" : "dbo";

    private static string TableCountSql(TestDatabaseProvider provider, string table) =>
        $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{DefaultSchema(provider)}' AND table_name = '{table}'";

    private static string SchemaTableCountSql(string schema) =>
        $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{schema}'";

    private static string DatabaseCountSql(TestDatabaseProvider provider, string name) => provider switch
    {
        TestDatabaseProvider.PostgreSql => $"SELECT COUNT(*) FROM pg_database WHERE datname = '{name}'",
        TestDatabaseProvider.SqlServer => $"SELECT COUNT(*) FROM sys.databases WHERE name = '{name}'",
        _ => throw Unsupported(provider),
    };

    private static Target On(DatabaseInfrastructureFixture database) => new(database.Provider, database.ConnectionString);

    private static Target Server(DbIntegrationFixture fixture) => new(fixture.Provider, fixture.ServerConnectionString);

    /// <summary>A connection string and the provider that reads it.</summary>
    private sealed record Target(TestDatabaseProvider Provider, string ConnectionString)
    {
        public async Task ExecuteAsync(string sql, CancellationToken ct)
        {
            await using var connection = Connect();
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<object?> ScalarAsync(string sql, CancellationToken ct)
        {
            await using var connection = Connect();
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync(ct);
        }

        private DbConnection Connect() => Provider switch
        {
            TestDatabaseProvider.PostgreSql => new NpgsqlConnection(ConnectionString),
            TestDatabaseProvider.SqlServer => new SqlConnection(ConnectionString),
            _ => throw Unsupported(Provider),
        };

        // A record prints every member, the connection string and its password with it.
        public override string ToString() => $"{Provider} connection";
    }

    private static ArgumentOutOfRangeException Unsupported(TestDatabaseProvider provider) =>
        new(nameof(provider), provider, "Unsupported test database provider.");
}
