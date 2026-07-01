using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Endatix.Persistence.PostgreSql.Setup;
using Endatix.Persistence.SqlServer.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// Builds a production-like service provider for core DbContext startup migration tests.
/// </summary>
internal static class IntegrationCoreMigrationTestHelper
{
    internal static IServiceProvider BuildServiceProvider(
        string connectionString,
        TestDatabaseProvider provider)
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider.ToString()
            })
            .Build();
        services.AddSingleton(configuration);
        RegisterCoreContextDependencies(services);

        switch (provider)
        {
            case TestDatabaseProvider.PostgreSql:
                services.AddPostgreSqlPersistence<AppDbContext>();
                services.AddPostgreSqlPersistence<AppIdentityDbContext>();
                break;
            case TestDatabaseProvider.SqlServer:
                services.AddSqlServerPersistence<AppDbContext>();
                services.AddSqlServerPersistence<AppIdentityDbContext>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.");
        }

        return services.BuildServiceProvider();
    }

    private static void RegisterCoreContextDependencies(IServiceCollection services)
    {
        services.AddSingleton<IIdGenerator<long>, NoOpIdGenerator>();
        services.AddSingleton<ITenantContext, BypassTenantContext>();
        services.AddSingleton(sp => new EfCoreValueGeneratorFactory(sp.GetRequiredService<IIdGenerator<long>>()));
        services.AddSingleton<OutboxIntegrationEventDispatcher>();
    }

    internal static async Task<bool> TableExistsAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string schema,
        string table,
        CancellationToken cancellationToken)
    {
        await using var connection = provider switch
        {
            TestDatabaseProvider.PostgreSql => (System.Data.Common.DbConnection)new Npgsql.NpgsqlConnection(connectionString),
            TestDatabaseProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        await connection.OpenAsync(cancellationToken);

        string sql = provider switch
        {
            TestDatabaseProvider.PostgreSql =>
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)",
            TestDatabaseProvider.SqlServer =>
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table) THEN 1 ELSE 0 END",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var schemaParam = command.CreateParameter();
        schemaParam.ParameterName = "@schema";
        schemaParam.Value = schema;
        command.Parameters.Add(schemaParam);
        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@table";
        tableParam.Value = table;
        command.Parameters.Add(tableParam);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool boolResult ? boolResult : Convert.ToInt32(result) == 1;
    }

    internal static async Task<bool> RoutineExistsAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string schema,
        string routineName,
        CancellationToken cancellationToken)
    {
        var sql = provider switch
        {
            TestDatabaseProvider.PostgreSql =>
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.routines
                    WHERE routine_schema = @schema
                      AND routine_name = @routine)
                """,
            TestDatabaseProvider.SqlServer =>
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM INFORMATION_SCHEMA.ROUTINES
                    WHERE ROUTINE_SCHEMA = @schema
                      AND ROUTINE_NAME = @routine)
                THEN 1 ELSE 0 END
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        return await ExecuteExistsQueryAsync(connectionString, provider, sql, schema, routineName, cancellationToken);
    }

    internal static async Task<bool> SqlRowExistsAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string sql,
        CancellationToken cancellationToken)
    {
        await using System.Data.Common.DbConnection connection = provider switch
        {
            TestDatabaseProvider.PostgreSql => new Npgsql.NpgsqlConnection(connectionString),
            TestDatabaseProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool boolResult ? boolResult : Convert.ToInt32(result) == 1;
    }

    private static async Task<bool> ExecuteExistsQueryAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string sql,
        string schema,
        string name,
        CancellationToken cancellationToken)
    {
        await using System.Data.Common.DbConnection connection = provider switch
        {
            TestDatabaseProvider.PostgreSql => new Npgsql.NpgsqlConnection(connectionString),
            TestDatabaseProvider.SqlServer => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        await connection.OpenAsync(cancellationToken);

        await using System.Data.Common.DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        var schemaParam = command.CreateParameter();
        schemaParam.ParameterName = "@schema";
        schemaParam.Value = schema;
        command.Parameters.Add(schemaParam);

        var nameParam = command.CreateParameter();
        nameParam.ParameterName = "@routine";
        nameParam.Value = name;
        command.Parameters.Add(nameParam);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool boolResult ? boolResult : Convert.ToInt32(result) == 1;
    }

    private sealed class NoOpIdGenerator : IIdGenerator<long>
    {
        public long CreateId() => 0;
    }

    private sealed class BypassTenantContext : ITenantContext
    {
        public long TenantId => 0;
    }
}
