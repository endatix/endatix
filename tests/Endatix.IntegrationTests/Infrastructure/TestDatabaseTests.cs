using System.Data.Common;
using Endatix.IntegrationTests.Shared;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.IntegrationTests;

/// <summary>
/// Every provider's tests run on a user database named alike, never on one of the server's system databases, whose
/// shipped tables and settings are not the ones a product database has.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class TestDatabaseTests(DbIntegrationFixture fixture)
{
    [Fact]
    public async Task Fixture_connects_to_the_endatix_test_database()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var sql = fixture.Provider switch
        {
            TestDatabaseProvider.PostgreSql => "SELECT current_database()",
            TestDatabaseProvider.SqlServer => "SELECT DB_NAME()",
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture.Provider, "Unsupported test database provider."),
        };
        await using DbConnection connection = fixture.Provider == TestDatabaseProvider.SqlServer
            ? new SqlConnection(fixture.ConnectionString)
            : new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Act
        var database = await command.ExecuteScalarAsync(ct);

        // Assert
        database.Should().Be("endatix_test");
    }
}
