extern alias EndatixWebHost;

using System.Data.Common;
using System.Net;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;

namespace Endatix.IntegrationTests.FeatureFlows.Jobs;

/// <summary>
/// The scheduler's operator dashboard: unmapped unless enabled, platform admins only, read-only by default.
/// </summary>
[Collection(nameof(JobsHostTestCollection))]
[Trait("Category", "FeatureFlow")]
[Trait("Priority", "P1")]
[Trait("Lane", "Jobs")]
public sealed class QuartzDashboardFlowTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SeedPassword = "Password123!";

    [Fact]
    public async Task Quartz_dashboard_is_unmapped_by_default()
    {
        // Arrange
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admitted_scheduler_requests_check_standing_once_and_respect_write_mode(bool allowWrites)
    {
        // Arrange — a job of a type no host handles, so its trigger stays waiting for the whole test.
        var cancellationToken = TestContext.Current.CancellationToken;
        var snapshots = AdmittedSnapshots();
        await using var host = new DashboardHost(fixture, allowWrites, snapshots);
        var world = await host.PrepareWorldAsync(
            IntegrationWorldOptions.SingleTenant with { DefaultPassword = SeedPassword }, cancellationToken);
        var jobId = await EnqueueWaitingJobAsync(host, world.SeedResult!.TenantIds[0], cancellationToken);
        (await TriggerStateAsync(jobId, cancellationToken)).Should().Be("WAITING");

        using var platformAdmin = await world.AsAsync(TestPersona.PlatformAdmin, cancellationToken: cancellationToken);
        snapshots.ClearReceivedCalls();

        // Act
        using var read = await platformAdmin.GetAsync(
            new Uri("/quartz-api/schedulers/endatix-jobs", UriKind.Relative), cancellationToken);
        read.EnsureSuccessStatusCode();
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        snapshots.ClearReceivedCalls();
        using var response = await platformAdmin.PostAsync(
            new Uri($"/quartz-api/schedulers/endatix-jobs/triggers/DashboardReadOnlyProbe/{jobId}/pause", UriKind.Relative),
            content: null,
            cancellationToken);

        // Assert
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        if (allowWrites)
        {
            response.EnsureSuccessStatusCode();
            (await TriggerStateAsync(jobId, cancellationToken)).Should().Be("PAUSED");
        }
        else
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.MethodNotAllowed);
            (await TriggerStateAsync(jobId, cancellationToken)).Should().Be("WAITING");
        }
    }

    private static async Task<long> EnqueueWaitingJobAsync(DashboardHost host, long tenantId, CancellationToken cancellationToken)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>().EnqueueAsync(
            new BackgroundJobRequest("DashboardReadOnlyProbe", "{}", tenantId), cancellationToken);
    }

    private static ICurrentUserAuthorizationService AdmittedSnapshots()
    {
        var snapshots = Substitute.For<ICurrentUserAuthorizationService>();
        snapshots.GetAuthorizationDataAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(AuthorizationData.ForAuthenticatedUser("operator", 1, [], [])));
        return snapshots;
    }

    [Fact]
    public async Task Scheduler_authorization_precedes_standing_and_static_assets_stay_open()
    {
        var ct = TestContext.Current.CancellationToken;
        var snapshots = AdmittedSnapshots();
        await using var host = new DashboardHost(fixture, allowWrites: false, snapshots);
        var world = await host.PrepareWorldAsync(IntegrationWorldOptions.SingleTenant, ct);
        using var admin = await world.AsAsync(TestPersona.PlatformAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        using var member = await world.AsAsync(TestPersona.TenantAdmin, mode: IntegrationAuthMode.SyntheticJwt, cancellationToken: ct);
        snapshots.ClearReceivedCalls();

        using var denied = await member.PostAsync("/quartz/hub/negotiate", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var deniedApi = await member.GetAsync("/quartz-api/schedulers", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedApi.StatusCode);
        using var script = await admin.GetAsync("/_framework/blazor.web.js", ct);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        await snapshots.DidNotReceive().GetAuthorizationDataAsync(Arg.Any<CancellationToken>());

        using var page = await admin.GetAsync("/quartz", ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
        snapshots.ClearReceivedCalls();
        using var negotiation = await admin.PostAsync("/quartz/hub/negotiate", null, ct);
        Assert.Equal(HttpStatusCode.OK, negotiation.StatusCode);
        await snapshots.Received(1).GetAuthorizationDataAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quartz_dashboard_gets_the_blazor_framework_script_it_needs_to_be_interactive()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = new DashboardHost(fixture, allowWrites: false);
        using var client = host.CreateClient();

        // Act
        using var response = await client.GetAsync(new Uri("/_framework/blazor.web.js", UriKind.Relative), cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("text/javascript");
        (await response.Content.ReadAsStringAsync(cancellationToken)).Should().NotBeEmpty();
    }

    private async Task<string?> TriggerStateAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var connection = ConnectionTo(fixture.Database.Provider, fixture.Database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT TRIGGER_STATE FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_NAME = '{jobId}'";
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private static DbConnection ConnectionTo(TestDatabaseProvider provider, string connectionString) => provider switch
    {
        TestDatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
        TestDatabaseProvider.SqlServer => new SqlConnection(connectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };

    /// <summary>A second web host on the same database, with the dashboard enabled.</summary>
    private sealed class DashboardHost : IIntegrationTestHostFixture, IAsyncDisposable
    {
        private readonly WebApplicationFactory<EndatixWebHost::Program> _factory;

        public DashboardHost(EndatixIntegrationWebHostFixture fixture, bool allowWrites, ICurrentUserAuthorizationService? snapshots = null)
        {
            Database = fixture.Database;
            _factory = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider)
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("Endatix:BackgroundJobs:Dashboard:Enabled", "true");
                    builder.UseSetting("Endatix:BackgroundJobs:Dashboard:AllowWrites", allowWrites.ToString());
                    // The host runs from build output, where no web root holds the assets yet: outside Development
                    // only publish copies them there, so without this they are found by route but not on disk.
                    builder.UseStaticWebAssets();
                    if (snapshots is not null)
                    {
                        builder.ConfigureTestServices(services => services.AddTransient(_ => new AccountStanding(snapshots)));
                    }
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
