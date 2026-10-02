using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.Modules;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class QuartzRegistrationTests
{
    private static readonly string[] ReportingJobTypes =
    [
        "ReportingCompileFormSchema",
        "ReportingFlattenSubmission",
        "ReportingSeedDefaultExportFormats",
        "ReportingSyncFormDeletion",
        "ReportingSyncSubmissionDeletion",
    ];

    [Fact]
    public void AddJobsScheduler_InvalidRetentionCron_LeavesTheErrorToOptionsValidation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=jobs",
                ["Endatix:BackgroundJobs:Retention:Cron"] = "every 15 minutes",
            })
            .Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        var register = () => services.AddJobsScheduler(configuration);

        // Assert — registration succeeds, and startup fails later with the validator's message naming the key.
        register.Should().NotThrow();
    }

    [Fact]
    public void Build_RegisteredJobTypes_PoolSizeIsSumOfCaps()
    {
        // Arrange
        var options = Bind(new Dictionary<string, string?>
        {
            ["JobTypes:WebHookDelivery:MaxConcurrency"] = "4",
            ["JobTypes:ReportingCompileFormSchema:MaxConcurrency"] = "2",
            ["JobTypes:ReportingFlattenSubmission:MaxConcurrency"] = "2",
            ["JobTypes:ReportingSeedDefaultExportFormats:MaxConcurrency"] = "2",
            ["JobTypes:ReportingSyncFormDeletion:MaxConcurrency"] = "2",
            ["JobTypes:ReportingSyncSubmissionDeletion:MaxConcurrency"] = "2",
        });
        string[] jobTypes = ["WebHookDelivery", .. ReportingJobTypes];

        // Act
        var plan = QuartzRegistration.Build(jobTypes, options);

        // Assert
        plan.PoolSize.Should().Be(14);
    }

    [Fact]
    public void Build_GlobalMaxConcurrencyConfigured_BindsToNothing()
    {
        // Arrange — the pool used to be one global number; that key no longer sizes anything.
        var options = Bind(new Dictionary<string, string?> { ["MaxConcurrency"] = "4" });

        // Act
        var plan = QuartzRegistration.Build(["A", "B"], options);

        // Assert — two job types at the default cap of one each.
        plan.PoolSize.Should().Be(2);
    }

    [Fact]
    public void Build_EveryJobTypeCappedAtZero_KeepsOneThread()
    {
        // Arrange
        var options = Bind(new Dictionary<string, string?> { ["JobTypes:A:MaxConcurrency"] = "0" });

        // Act
        var plan = QuartzRegistration.Build(["A"], options);

        // Assert
        plan.PoolSize.Should().Be(1);
    }

    [Fact]
    public void AddJobsScheduler_RegisteredJobTypes_BatchSizeIsTheThreadPoolSize()
    {
        // Arrange — caps of three and one, plus the retention thread.
        using var provider = SchedulerServices(
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:JobTypes:A:MaxConcurrency"] = "3" },
            "A", "B");

        // Act
        var batchSize = SchedulerOptions<QuartzSchedulerOptions>(provider).MaxBatchSize;
        var threads = SchedulerOptions<ThreadPoolOptions>(provider).MaxConcurrency;

        // Assert
        threads.Should().Be(5);
        batchSize.Should().Be(threads);
    }

    [Fact]
    public void AddJobsScheduler_Store_InsertsTriggersWithoutTheTriggerLock()
    {
        // Arrange
        using var provider = SchedulerServices(new Dictionary<string, string?>(), "A");

        // Act
        var lockOnInsert = SchedulerOptions<AdoJobStoreOptions>(provider).LockOnInsert;

        // Assert
        lockOnInsert.Should().BeFalse();
    }

    // The module's own registration, with a handler registry of the given job types in place of the host's handlers.
    private static ServiceProvider SchedulerServices(Dictionary<string, string?> settings, params string[] jobTypes)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=jobs",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "postgresql",
            })
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        JobsModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));
        services.Replace(ServiceDescriptor.Singleton(JobHandlerRegistry.Build(jobTypes.Select(Handler))));
        return services.BuildServiceProvider();
    }

    private static IBackgroundJobHandler Handler(string jobType)
    {
        var handler = Substitute.For<IBackgroundJobHandler>();
        handler.JobType.Returns(jobType);
        return handler;
    }

    private static T SchedulerOptions<T>(IServiceProvider provider) where T : class =>
        provider.GetRequiredService<IOptionsMonitor<T>>().Get(QuartzRegistration.SchedulerName);

    private static BackgroundJobsOptions Bind(Dictionary<string, string?> section)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(section.Select(entry =>
                KeyValuePair.Create($"{BackgroundJobsOptions.SectionName}:{entry.Key}", entry.Value)))
            .Build();

        var options = new BackgroundJobsOptions();
        configuration.GetSection(BackgroundJobsOptions.SectionName).Bind(options);
        return options;
    }
}
