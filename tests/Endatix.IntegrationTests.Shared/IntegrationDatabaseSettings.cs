namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Settings for the integration test database, including provider selection and optional custom Docker images.
/// </summary>
public sealed record IntegrationDatabaseSettings(
    TestDatabaseProvider Provider,
    EndatixTestcontainersSettings ContainerSettings,
    string? PostgreSqlImage = null,
    string? SqlServerImage = null)
{
    /// <summary>
    /// Reads database settings from environment variables (<c>ENDATIX_TEST_DB_PROVIDER</c>, <c>ENDATIX_TEST_DB_POSTGRES_IMAGE</c>, <c>ENDATIX_TEST_DB_SQLSERVER_IMAGE</c>).
    /// </summary>
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

        return new IntegrationDatabaseSettings(
            provider,
            EndatixTestcontainersSettings.FromEnvironment(),
            postgresImage,
            sqlServerImage);
    }
}
