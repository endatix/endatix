using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.Modules;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Modules.Jobs.Tests;

/// <summary>
/// Resolves the queue through the module's own registrations. The other queue tests construct it by hand, which
/// cannot show whether the container treats an unregistered optional seam as absent or as a resolution failure.
/// </summary>
public class JobsModuleQueueResolutionTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetRequiredService_NoStrategyOrMetricsRegistered_ResolvesTheQueue()
    {
        // Arrange
        var services = ModuleServices(Substitute.For<IJobsDbContext>());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        // Act
        var queue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();

        // Assert
        queue.Should().BeOfType<BackgroundJobQueue>();
    }

    [Fact]
    public async Task GetRequiredService_StrategyAndMetricsRegistered_PassesThemToTheQueue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestJobsDbContext>()
            .UseInMemoryDatabase($"jobs-{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new TestJobsDbContext(options, new FixedTenantContext(0));
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Returns(true);
        var metrics = Substitute.For<IJobMetrics>();
        var services = ModuleServices(dbContext);
        services.AddSingleton(dispatchStrategy);
        services.AddSingleton(metrics);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var queue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
        var jobId = await queue.EnqueueAsync(
            new BackgroundJobRequest("SubmissionExport", """{"formId":"1"}""", 7),
            TestContext.Current.CancellationToken);

        // Assert
        dispatchStrategy.Received(1).TryOffer(new JobDispatchItem(jobId, "SubmissionExport"));
        metrics.Received(1).Record(JobLifecycleEvent.Enqueued, "SubmissionExport");
    }

    // Runs the module's own registration, then supplies what a host would: the module's context needs a PostgreSQL
    // server, and the clock comes from the host's infrastructure registrations rather than from the module.
    private static ServiceCollection ModuleServices(IJobsDbContext dbContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=endatix;Username=endatix;Password=endatix",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "postgresql",
            })
            .Build();
        var services = new ServiceCollection();
        JobsModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));

        services.Replace(ServiceDescriptor.Scoped<IJobsDbContext>(_ => dbContext));

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(Now));
        services.AddSingleton(clock);

        return services;
    }
}
