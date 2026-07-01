namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Supported database providers for integration tests.
/// </summary>
public enum TestDatabaseProvider
{
    /// <summary>PostgreSQL via Testcontainers.</summary>
    PostgreSql,
    /// <summary>SQL Server via Testcontainers.</summary>
    SqlServer
}
