namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Docker labels applied to Endatix Testcontainers resources for filtering and debugging.
/// </summary>
public static class EndatixTestcontainerLabels
{
    /// <summary>Docker label for the test suite.</summary>
    public const string Suite = "endatix.test.suite";

    /// <summary>Docker label for the test component (postgres, sqlserver, keycloak, network).</summary>
    public const string Component = "endatix.test.component";

    /// <summary>Docker label for the test run correlation id.</summary>
    public const string Run = "endatix.test.run";

    /// <summary>
    /// Groups containers under one stack in Docker Desktop (same label as Docker Compose projects).
    /// </summary>
    public const string DockerComposeProject = "com.docker.compose.project";

    /// <summary>Value for the suite label — always "integration".</summary>
    public const string SuiteValue = "integration";

    /// <summary>Component label value for PostgreSQL containers.</summary>
    public const string ComponentPostgres = "postgres";

    /// <summary>Component label value for SQL Server containers.</summary>
    public const string ComponentSqlServer = "sqlserver";

    /// <summary>Component label value for Keycloak containers.</summary>
    public const string ComponentKeycloak = "keycloak";

    /// <summary>Component label value for the Docker network.</summary>
    public const string ComponentNetwork = "network";
}
