using Endatix.Infrastructure.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Endatix.Hosting.Tests.HealthChecks;

/// <summary>
/// Dummy type used to simulate Aspire presence (IsAspireServiceDefaultsPresent checks for "OpenTelemetry" in service type FullName).
/// </summary>
internal sealed class OpenTelemetryMarker;

/// <summary>
/// Component tests for health check builder extensibility and default behaviour.
/// Builds the DI container and runs health checks in-process (no HTTP).
/// </summary>
public sealed class EndatixHealthChecksBuilderTests
{
    private static readonly Dictionary<string, string?> _minimalConfig = new()
    {
        ["Endatix:Auth:DefaultScheme"] = InfrastructureSecurityBuilder.MULTI_JWT_SCHEME_NAME,
        ["Endatix:Auth:Providers:EndatixJwt:Issuer"] = "test",
        ["Endatix:Auth:Providers:EndatixJwt:SigningKey"] = "test-signing-key-32-characters-long",
        ["Endatix:Auth:Providers:EndatixJwt:Audiences:0"] = "test",
        ["Endatix:Auth:Providers:EndatixJwt:Enabled"] = "true"
    };
    private static IConfiguration CreateMinimalConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(_minimalConfig)
            .Build();
    }

    [Fact]
    public async Task ConfigureEndatix_WithCustomHealthCheck_ReportContainsCustomCheck()
    {
        // Arrange
        var config = CreateMinimalConfig();
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((context, services) =>
            {
                var builder = services.AddEndatix(context.Configuration);
                builder.HealthChecks.UseDefaults();
                builder.HealthChecks.AddCheck("my-service", () => HealthCheckResult.Healthy("My service is healthy"));
                // Required so FinalizeConfiguration() -> Infrastructure.Build() -> Security.Build() succeeds
                builder.Infrastructure.Security.UseDefaults();
                builder.FinalizeConfiguration();
            })
            .Build();

        // Act
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        report.Status.Should().Be(HealthStatus.Healthy);
        report.Entries.Should().ContainKey("self");
        report.Entries.Should().ContainKey("my-service");
        report.Entries["my-service"].Status.Should().Be(HealthStatus.Healthy);
        report.Entries["my-service"].Description.Should().Be("My service is healthy");
    }

    [Fact]
    public async Task ConfigureEndatix_UseDefaultsOnly_ReportContainsSelfCheck()
    {
        // Arrange
        var config = CreateMinimalConfig();
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((context, services) =>
            {
                var builder = services.AddEndatix(context.Configuration);
                builder.HealthChecks.UseDefaults();
                // Required so FinalizeConfiguration() -> Infrastructure.Build() -> Security.Build() succeeds
                builder.Infrastructure.Security.UseDefaults();
                builder.FinalizeConfiguration();
            })
            .Build();

        // Act
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        report.Entries.Should().ContainKey("self");
        report.Entries["self"].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task ConfigureEndatix_WhenAspireServiceDefaultsPresent_SelfCheckIsNotAdded()
    {
        // Arrange
        var config = CreateMinimalConfig();
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((context, services) =>
            {
                // Simulate Aspire presence so UseDefaults() skips the "self" check
                services.AddSingleton<OpenTelemetryMarker>();
                var builder = services.AddEndatix(context.Configuration);
                builder.HealthChecks.UseDefaults();
                builder.Infrastructure.Security.UseDefaults();
                builder.FinalizeConfiguration();
            })
            .Build();

        // Act
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        report.Entries.Should().NotContainKey("self");
    }

    [Fact]
    public async Task ConfigureEndatix_UseDefaults_ReportContainsDatabaseAndIdentityDatabaseChecks()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(_minimalConfig)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=EndatixHealthCheckTest;Trusted_Connection=True;TrustServerCertificate=True" })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((context, services) =>
            {
                var builder = services.AddEndatix(context.Configuration);
                builder.UseDefaults();
                builder.FinalizeConfiguration();
            })
            .Build();

        // Act
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        // Assert
        report.Entries.Should().ContainKey("database");
        report.Entries.Should().ContainKey("identity-database");
    }
}
