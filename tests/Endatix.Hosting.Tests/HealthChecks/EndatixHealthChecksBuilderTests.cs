using Endatix.Hosting.Builders;
using Endatix.Hosting.HealthChecks;
using Endatix.Infrastructure.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Endatix.Hosting.Tests.HealthChecks;

/// <summary>
/// Dummy type used to simulate Aspire presence (IsAspireServiceDefaultsPresent checks for
/// "ServiceDiscovery" in service type FullName).
/// </summary>
internal sealed class ServiceDiscoveryMarker;

/// <summary>
/// Dummy type standing in for Endatix's own OpenTelemetry registration. It must NOT be mistaken for
/// Aspire. The detection is gone now; the process check is always registered.
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
        report.Entries.Should().ContainKey(EndatixHealthChecksBuilder.SelfCheckName);
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
        report.Entries.Should().ContainKey(EndatixHealthChecksBuilder.SelfCheckName);
        report.Entries[EndatixHealthChecksBuilder.SelfCheckName].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task ConfigureEndatix_WhenServiceDiscoveryRegistered_SelfCheckIsStillAdded()
    {
        // Arrange
        var config = CreateMinimalConfig();
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((context, services) =>
            {
                // A service-discovery registration used to suppress the process check via a substring match
                services.AddSingleton<ServiceDiscoveryMarker>();
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
        report.Entries.Should().ContainKey(EndatixHealthChecksBuilder.SelfCheckName,
            "the process check is registered unconditionally now — Aspire detection was a substring match that could false-positive and leave liveness with nothing to evaluate");
    }

    [Fact]
    public async Task ConfigureEndatix_WithOpenTelemetryRegistered_StillRegistersSelfCheck()
    {
        // Arrange. The Aspire probe used to match any service whose type name contained
        // "OpenTelemetry", so Endatix registering its own SDK would silently disable this check.
        var config = CreateMinimalConfig();
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, c) => c.AddConfiguration(config))
            .ConfigureServices((context, services) =>
            {
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
        report.Entries.Should().ContainKey(EndatixHealthChecksBuilder.SelfCheckName);
        report.Entries[EndatixHealthChecksBuilder.SelfCheckName].Status.Should().Be(HealthStatus.Healthy);
    }

    /// <summary>
    /// The predicate is the whole safety property of the liveness probe, and the end-to-end test
    /// cannot catch a regression that still leaves /alive green. Assert the filtering directly.
    /// </summary>
    [Fact]
    public async Task CheckHealth_WithLivenessPredicate_ExcludesDatabaseChecks()
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
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        // Act
        var report = await healthCheckService.CheckHealthAsync(
            HealthCheckOptionsFactory.IsLivenessCheck,
            TestContext.Current.CancellationToken);

        // Assert
        report.Entries.Should().ContainKey(EndatixHealthChecksBuilder.SelfCheckName);
        report.Entries.Should().NotContainKey("database", "liveness must not depend on the database");
        report.Entries.Should().NotContainKey("identity-database");
    }

    [Fact]
    public async Task CheckHealth_WithReadinessPredicate_ContainsOnlyDatabaseChecks()
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
                // An untagged check, as a consumer would add: it must not reach the readiness probe,
                // or an unrelated dependency starts evicting pods from the Service endpoints.
                builder.HealthChecks.AddCheck("consumer-check", () => HealthCheckResult.Healthy());
                builder.FinalizeConfiguration();
            })
            .Build();
        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();

        // Act
        var report = await healthCheckService.CheckHealthAsync(
            HealthCheckOptionsFactory.IsReadinessCheck,
            TestContext.Current.CancellationToken);

        // Assert
        report.Entries.Should().ContainKey("database");
        report.Entries.Should().NotContainKey(EndatixHealthChecksBuilder.SelfCheckName,
            "the process check does not gate traffic");
        report.Entries.Should().NotContainKey("consumer-check", "only checks tagged 'ready' gate traffic");
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
