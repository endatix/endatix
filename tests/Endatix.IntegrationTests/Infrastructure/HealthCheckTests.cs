using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Endatix.Hosting.HealthChecks;
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
        var ready = await client.GetAsync(new Uri("/ready", UriKind.Relative), cancellationToken);
        var health = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        alive.StatusCode.Should().Be(HttpStatusCode.OK, "liveness must not depend on the database");
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "readiness must fail when the database is unreachable");
        health.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "the unfiltered report includes the database");
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

    [Fact]
    public async Task Readiness_endpoint_honours_a_custom_path()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:Hosting:ReadinessPath", "/custom-ready"));
        var client = factory.CreateClient();

        // Act
        var custom = await client.GetAsync(new Uri("/custom-ready", UriKind.Relative), cancellationToken);
        var standard = await client.GetAsync(new Uri("/ready", UriKind.Relative), cancellationToken);

        // Assert
        custom.StatusCode.Should().Be(HttpStatusCode.OK);
        standard.StatusCode.Should().Be(HttpStatusCode.NotFound, "the configured path replaces the default");
    }

    [Fact]
    public async Task Health_endpoint_honours_a_custom_path()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:Hosting:HealthCheckPath", "/custom-health"));
        var client = factory.CreateClient();

        // Act
        var custom = await client.GetAsync(new Uri("/custom-health", UriKind.Relative), cancellationToken);
        var detail = await client.GetAsync(new Uri("/custom-health/detail", UriKind.Relative), cancellationToken);
        var standard = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);

        // Assert
        custom.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.StatusCode.Should().Be(HttpStatusCode.OK, "the detail view follows the configured base path");
        standard.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Readiness must fail closed. Reachable in production: the database checks are registered only
    /// when a DbContext is already in Services, so a host calling HealthChecks.UseDefaults() before
    /// or without persistence maps /ready with nothing to evaluate — and an empty predicate reports
    /// Healthy, which would keep every pod in the Service endpoints while requests 500.
    /// </summary>
    [Fact]
    public async Task Readiness_endpoint_reports_unhealthy_when_no_readiness_check_is_registered()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    var readiness = options.Registrations
                        .Where(registration => registration.Tags.Contains(HealthCheckTags.Ready))
                        .ToList();

                    foreach (var registration in readiness)
                    {
                        options.Registrations.Remove(registration);
                    }
                })));
        var client = factory.CreateClient();

        // Act
        var ready = await client.GetAsync(new Uri("/ready", UriKind.Relative), cancellationToken);
        var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);

        // Assert
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            "readiness with nothing to check must not pass by default");
        alive.StatusCode.Should().Be(HttpStatusCode.OK,
            "liveness is unaffected — failing it closed would restart-loop the container");
    }

    /// <summary>
    /// Probes are mapped before the diagnostic views and the first registration wins, so a liveness
    /// path equal to a derived view shadows it silently rather than colliding visibly.
    /// </summary>
    [Fact]
    public async Task Startup_fails_when_a_probe_path_shadows_a_diagnostic_view()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting("Endatix:Hosting:LivenessPath", "/health/detail"));

        // Act
        var act = async () =>
        {
            var client = factory.CreateClient();
            await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);
        };

        // Assert
        await act.Should().ThrowAsync<Exception>("the JSON view would be shadowed by the liveness probe");
    }

    [Theory]
    [InlineData("Endatix:Hosting:HealthCheckPath", "", "empty path matches every request")]
    [InlineData("Endatix:Hosting:LivenessPath", "alive", "a path without a leading slash throws from inside Map")]
    [InlineData("Endatix:Hosting:ReadinessPath", "/health", "a duplicate path is silently ignored, first registration wins")]
    public async Task Startup_fails_when_a_probe_path_is_invalid(string setting, string value, string reason)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var factory = new EndatixWebApplicationFactory(
                _fixture.Database.ConnectionString,
                _fixture.Database.Provider)
            .WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        // Act
        var act = async () =>
        {
            var client = factory.CreateClient();
            await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);
        };

        // Assert
        (await act.Should().ThrowAsync<Exception>(reason))
            .Which.Should().Match<Exception>(
                exception => Flatten(exception).Contains(setting, StringComparison.Ordinal),
                "the failure must name the offending option");
    }

    private static string Flatten(Exception exception)
    {
        var text = exception.ToString();
        return exception is AggregateException aggregate
            ? text + string.Join(' ', aggregate.InnerExceptions.Select(inner => inner.ToString()))
            : text;
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
