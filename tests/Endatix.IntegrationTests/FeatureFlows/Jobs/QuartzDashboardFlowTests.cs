extern alias EndatixWebHost;

using System.Net;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Endatix.IntegrationTests.FeatureFlows.Jobs;

/// <summary>
/// The scheduler's operator dashboard: unmapped unless enabled, platform admins only, read-only by default.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class QuartzDashboardFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    [Fact]
    public async Task Quartz_dashboard_is_unmapped_by_default()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await fixture.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, cancellationToken);
        using var client = await world.AsAsync(TestPersona.PlatformAdmin, cancellationToken: cancellationToken);

        // Act
        using var response = await client.GetAsync(new Uri("/quartz", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Quartz_dashboard_refuses_non_platform_admins()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = new DashboardHost(fixture, allowWrites: false);
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, cancellationToken);
        using var tenantAdmin = await world.AsAsync(TestPersona.TenantAdmin, cancellationToken: cancellationToken);
        using var anonymous = world.AnonymousClient();

        // Act
        using var asTenantAdmin = await tenantAdmin.GetAsync(new Uri("/quartz", UriKind.Relative), cancellationToken);
        using var asAnonymous = await anonymous.GetAsync(new Uri("/quartz", UriKind.Relative), cancellationToken);

        // Assert
        asTenantAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        asAnonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Quartz_dashboard_is_read_only_by_default()
    {
        // Arrange — a job of a type no host handles, so its trigger stays waiting for the whole test.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = new DashboardHost(fixture, allowWrites: false);
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, cancellationToken);
        long jobId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            jobId = await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>().EnqueueAsync(
                new BackgroundJobRequest("DashboardReadOnlyProbe", "{}", world.SeedResult!.TenantIds[0]),
                cancellationToken);
        }

        using var platformAdmin = await world.AsAsync(TestPersona.PlatformAdmin, cancellationToken: cancellationToken);

        // Act
        using var read = await platformAdmin.GetAsync(
            new Uri("/quartz-api/schedulers/endatix-jobs", UriKind.Relative), cancellationToken);
        using var response = await platformAdmin.PostAsync(
            new Uri($"/quartz-api/schedulers/endatix-jobs/triggers/DashboardReadOnlyProbe/{jobId}/pause", UriKind.Relative),
            content: null,
            cancellationToken);

        // Assert — the platform admin may read, so the refusal is the read-only mode's, not authorization's.
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.MethodNotAllowed);
        (await TriggerStateAsync(jobId, cancellationToken)).Should().NotBe("PAUSED");
    }

    private async Task<string?> TriggerStateAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"SELECT trigger_state FROM jobs.qrtz_triggers WHERE trigger_name = '{jobId}'", connection);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>A second web host on the same database, with the dashboard enabled.</summary>
    private sealed class DashboardHost : IIntegrationTestHostFixture, IAsyncDisposable
    {
        private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

        public DashboardHost(EndatixIntegrationWebHostFixture fixture, bool allowWrites)
        {
            Database = fixture.Database;
            _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Endatix:BackgroundJobs:Dashboard:Enabled", "true");
                    builder.UseSetting("Endatix:BackgroundJobs:Dashboard:AllowWrites", allowWrites.ToString());
                });
            Seed = new IntegrationSeedBuilder(_factory.Services);
        }

        public DatabaseInfrastructureFixture Database { get; }

        public DatabaseCheckpoint Checkpoint => Database.Checkpoint;

        public IServiceProvider Services => _factory.Services;

        public IntegrationSeedBuilder Seed { get; }

        public HttpClient CreateClient() => _factory.CreateClient();

        public ValueTask DisposeAsync() => _factory.DisposeAsync();
    }
}
