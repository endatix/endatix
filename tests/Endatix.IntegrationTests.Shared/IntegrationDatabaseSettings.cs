namespace Endatix.IntegrationTests.Shared;

public sealed record IntegrationDatabaseSettings(
    TestDatabaseProvider Provider,
    string? PostgreSqlImage = null,
    string? SqlServerImage = null)
{
    public static IntegrationDatabaseSettings FromEnvironment()
    {
        var providerRaw = Environment.GetEnvironmentVariable("ENDATIX_TEST_DB_PROVIDER") ?? "PostgreSql";
        var parsed = Enum.TryParse(providerRaw, ignoreCase: true, out TestDatabaseProvider provider);
        if (!parsed)
        {
            throw new InvalidOperationException(
                $"Unsupported ENDATIX_TEST_DB_PROVIDER value '{providerRaw}'. Supported values: PostgreSql, SqlServer.");
        }

        var postgresImage = Environment.GetEnvironmentVariable("ENDATIX_TEST_DB_POSTGRES_IMAGE");
        var sqlServerImage = Environment.GetEnvironmentVariable("ENDATIX_TEST_DB_SQLSERVER_IMAGE");

        return new IntegrationDatabaseSettings(provider, postgresImage, sqlServerImage);
    }
}
