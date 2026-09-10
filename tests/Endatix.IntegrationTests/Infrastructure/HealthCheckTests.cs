using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P0")]
public sealed class HealthCheckTests
{
    private readonly EndatixIntegrationWebHostFixture _fixture;

    public HealthCheckTests(EndatixIntegrationWebHostFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Alive_endpoint_returns_success()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The reason the two probes exist. Liveness answers "is the process running?", so it must stay
    /// green while the database is down — a Kubernetes livenessProbe that goes red on a database
    /// blip restarts every pod at once, and restarting a process cannot fix a database.
    /// Readiness answers "can this pod serve traffic?", so it must go red and drop the pod out of
    /// the Service endpoints.
    /// </summary>
    [Fact]
    public async Task Probes_disagree_when_the_database_is_unreachable()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Auto-migrations off deliberately. The shared factory config turns them on, so CreateClient
        // would run DatabaseMigrationService against the dead host and survive only because that
        // service swallows its exception. This test would then start failing at CreateClient the day
        // that swallow is tightened, reading as a probe regression when nothing about probes changed.
        await using var factory = new EndatixWebApplicationFactory(
                UnreachableConnectionString(_fixture.Database.Provider),
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:Data:EnableAutoMigrations", "false"));
        var client = factory.CreateClient();

        // Act
        var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);
        var health = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        alive.StatusCode.Should().Be(HttpStatusCode.OK, "liveness must not depend on the database");
        health.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "readiness must fail when the database is unreachable");
    }

    [Fact]
    public async Task Liveness_endpoint_honours_a_custom_path()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:Hosting:LivenessPath", "/custom-alive"));
        var client = factory.CreateClient();

        // Act
        var custom = await client.GetAsync(new Uri("/custom-alive", UriKind.Relative), cancellationToken);
        var standard = await client.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);

        // Assert
        custom.StatusCode.Should().Be(HttpStatusCode.OK);
        standard.StatusCode.Should().Be(HttpStatusCode.NotFound, "the configured path replaces the default");
    }

    // Port 1 is reserved and nothing listens on it, so the connection fails fast rather than
    // hanging for the provider's default timeout.
    private static string UnreachableConnectionString(TestDatabaseProvider provider) =>
        provider == TestDatabaseProvider.SqlServer
            ? "Server=127.0.0.1,1;Database=endatix_down;User Id=sa;Password=Nope;TrustServerCertificate=True;Connect Timeout=2"
            : "Host=127.0.0.1;Port=1;Database=endatix_down;Username=postgres;Password=postgres;Timeout=2;Command Timeout=2";

    [Fact]
    public async Task Health_endpoint_returns_success_with_per_test_service_override()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IntegrationTestMarkerService>();
                });
            });
        var markerService = factory.Services.GetRequiredService<IntegrationTestMarkerService>();
        Assert.NotNull(markerService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
    }
}

internal sealed class IntegrationTestMarkerService;
