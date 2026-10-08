extern alias EndatixWebHost;

using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs.Handlers;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// Each job type runs with the settings its owner declares in code unless the host's configuration sets them: on a
/// host built on the packages rather than on <c>Endatix.WebHost</c>'s own appsettings files, on
/// <c>Endatix.WebHost</c> as shipped, and with a global setting the host configures.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class JobTypeDefaultsTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const string WebHook = "WebHookDelivery";
    private const string ReportingFlatten = "ReportingFlattenSubmission";

    private static readonly BackgroundJobTypePolicy TunedWebHook = new(
        MaxAttempts: 8,
        MaxRuntime: TimeSpan.FromMinutes(5),
        BackoffBase: TimeSpan.FromSeconds(10),
        BackoffCap: TimeSpan.FromSeconds(3600),
        MaxConcurrency: 4,
        Retention: TimeSpan.FromDays(3));

    private static readonly BackgroundJobTypePolicy TunedReporting = new(
        MaxAttempts: 5,
        MaxRuntime: TimeSpan.FromMinutes(10),
        BackoffBase: TimeSpan.FromSeconds(10),
        BackoffCap: TimeSpan.FromSeconds(600),
        MaxConcurrency: 2,
        Retention: TimeSpan.FromDays(3));

    [Fact]
    public async Task Webhook_jobs_run_four_at_a_time_on_a_host_without_job_settings()
    {
        // Arrange — the webhook handler is swapped for one that holds its slot, leaving the declared settings.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        var runs = new NamedProbeRuns();
        await using var host = HostWithoutJobSettings(database, services => ReplaceWebHookHandler(services, runs));

        // Act
        var webhooks = await EnqueueAsync(
            host.Services,
            Enumerable.Range(0, 6).Select(_ => NamedProbeHandler.Request(WebHook, holdMilliseconds: 20_000)).ToList(),
            ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(runs.Count >= 4), TimeSpan.FromSeconds(15), ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        var processing = await database.CountAsync(
            $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "Status" = 1 AND "Id" IN ({webhooks.IdList()})""", ct);

        // Assert
        host.Services.GetRequiredService<IConfiguration>().GetSection("Endatix:BackgroundJobs").GetChildren()
            .Should().BeEmpty();
        processing.Should().Be(4);
        runs.PeakConcurrency(WebHook).Should().Be(4);
    }

    [Fact]
    public async Task Reporting_jobs_get_their_declared_settings_and_threads_on_a_host_without_job_settings()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var host = HostWithoutJobSettings(database, _ => { });

        // Act
        var policies = host.Services.GetRequiredService<JobTypePolicies>();
        var plan = QuartzRegistration.Build(host.Services.GetRequiredService<JobHandlerRegistry>().JobTypes, policies);
        var threads = host.Services.GetRequiredService<IOptionsMonitor<ThreadPoolOptions>>()
            .Get(QuartzRegistration.SchedulerName).MaxConcurrency;

        // Assert — the pool holds every cap the execution limits apply, plus the retention thread.
        policies.For(ReportingFlatten).Should().Be(TunedReporting);
        plan.GroupCaps[WebHook].Should().Be(4);
        plan.GroupCaps[ReportingFlatten].Should().Be(2);
        threads.Should().Be(plan.PoolSize + 1);
    }

    [Fact]
    public async Task WebHost_with_its_shipped_settings_runs_webhook_and_reporting_jobs_as_tuned()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var host = StartedHost(database, ReportingOn);

        // Act
        var policies = host.Services.GetRequiredService<JobTypePolicies>();

        // Assert — the shipped appsettings set the general job settings, but none that replaces a tuned value.
        host.Services.GetRequiredService<IConfiguration>().GetSection("Endatix:BackgroundJobs:IdleWaitTimeSeconds")
            .Value.Should().NotBeNull("the shipped appsettings must have been loaded");
        policies.For(WebHook).Should().Be(TunedWebHook);
        policies.For(ReportingFlatten).Should().Be(TunedReporting);
    }

    [Fact]
    public async Task A_global_job_setting_the_host_configures_applies_to_the_tuned_job_types()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, ct);
        await using var host = StartedHost(database, builder =>
        {
            ReportingOn(builder);
            builder.UseSetting("Endatix:BackgroundJobs:RetentionDays", "30");
        });

        // Act
        var policies = host.Services.GetRequiredService<JobTypePolicies>();

        // Assert — the configured key replaces the declared retention; every other setting stays as declared.
        policies.For(WebHook).Should().Be(TunedWebHook with { Retention = TimeSpan.FromDays(30) });
        policies.For(ReportingFlatten).Should().Be(TunedReporting with { Retention = TimeSpan.FromDays(30) });
    }

    // The full web host with Reporting on and no appsettings file, so nothing configures a job type.
    private static WebApplicationFactory<EndatixWebHost::Program> HostWithoutJobSettings(
        JobsTestDatabase database,
        Action<IServiceCollection> configureServices) =>
        StartedHost(database, builder =>
        {
            ReportingOn(builder);
            builder.ConfigureAppConfiguration((_, configuration) => RemoveAppSettingsFiles(configuration));
            builder.ConfigureTestServices(configureServices);
        });

    private static WebApplicationFactory<EndatixWebHost::Program> StartedHost(
        JobsTestDatabase database,
        Action<IWebHostBuilder> configure)
    {
        var factory = new EndatixWebApplicationFactory(database.ConnectionString, TestDatabaseProvider.PostgreSql)
            .WithWebHostBuilder(configure);

        // Reading the services builds and starts the host, and with it the migrations and the scheduler.
        _ = factory.Services;
        return factory;
    }

    private static void ReportingOn(IWebHostBuilder builder) =>
        builder.UseSetting("Endatix:FeatureFlags:ReportingModule", "true");

    private static void RemoveAppSettingsFiles(IConfigurationBuilder configuration)
    {
        var appSettings = configuration.Sources.OfType<JsonConfigurationSource>()
            .Where(source => source.Path?.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        foreach (var source in appSettings)
        {
            configuration.Sources.Remove(source);
        }
    }

    private static void ReplaceWebHookHandler(IServiceCollection services, NamedProbeRuns runs)
    {
        var webHookHandlers = services.Where(IsWebHookHandler).ToList();
        foreach (var descriptor in webHookHandlers)
        {
            services.Remove(descriptor);
        }

        services.AddBackgroundJobHandler(WebHook, _ => new NamedProbeHandler(WebHook, runs));
    }

    private static bool IsWebHookHandler(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IBackgroundJobHandler)
        && (descriptor.IsKeyedService ? descriptor.KeyedImplementationType : descriptor.ImplementationType)
            == typeof(WebHookDeliveryJobHandler);

    private static async Task<IReadOnlyList<long>> EnqueueAsync(
        IServiceProvider services,
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>()
            .EnqueueManyAsync(requests, cancellationToken);
    }
}
