using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
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
        var plan = QuartzRegistration.Build(jobTypes, Configured(options));

        // Assert
        plan.PoolSize.Should().Be(14);
    }

    [Fact]
    public void Build_GlobalMaxConcurrencyConfigured_BindsToNothing()
    {
        // Arrange — the pool used to be one global number; that key no longer sizes anything.
        var options = Bind(new Dictionary<string, string?> { ["MaxConcurrency"] = "4" });

        // Act
        var plan = QuartzRegistration.Build(["A", "B"], Configured(options));

        // Assert — two job types at the default cap of one each.
        plan.PoolSize.Should().Be(2);
    }

    [Fact]
    public void Build_EveryJobTypeCappedAtZero_KeepsOneThread()
    {
        // Arrange
        var options = Bind(new Dictionary<string, string?> { ["JobTypes:A:MaxConcurrency"] = "0" });

        // Act
        var plan = QuartzRegistration.Build(["A"], Configured(options));

        // Assert
        plan.PoolSize.Should().Be(1);
    }

    [Fact]
    public void AddJobsScheduler_RegisteredJobTypes_BatchSizeIsTheThreadPoolSize()
    {
        // Arrange — caps of three and one, plus the retention thread.
        using var provider = SchedulerServices(
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:JobTypes:A:MaxConcurrency"] = "3" },
            ["A", "B"]);

        // Act
        var batchSize = SchedulerOptions<QuartzSchedulerOptions>(provider).MaxBatchSize;
        var threads = SchedulerOptions<ThreadPoolOptions>(provider).MaxConcurrency;

        // Assert
        threads.Should().Be(5);
        batchSize.Should().Be(threads);
    }

    [Fact]
    public void Build_DeclaredDefaultsAndNoConfiguration_CapsComeFromTheDeclaredDefaults()
    {
        // Arrange
        var reporting = new BackgroundJobTypeDefaults { MaxConcurrency = 2 };
        var services = new ServiceCollection()
            .AddBackgroundJobTypeDefaults("WebHookDelivery", DeclaredDefaults.Tuned);
        foreach (var jobType in ReportingJobTypes)
        {
            services.AddBackgroundJobTypeDefaults(jobType, reporting);
        }

        var policies = Policies(new BackgroundJobsOptions(), services);

        // Act
        var plan = QuartzRegistration.Build(["WebHookDelivery", .. ReportingJobTypes], policies);

        // Assert
        plan.GroupCaps["WebHookDelivery"].Should().Be(4);
        plan.GroupCaps["ReportingFlattenSubmission"].Should().Be(2);
        plan.PoolSize.Should().Be(14);
    }

    [Fact]
    public void AddJobsScheduler_DeclaredMaxConcurrency_SizesThreadPoolAndGroupCapsAlike()
    {
        // Arrange
        using var provider = SchedulerServices(
            new Dictionary<string, string?>(),
            ["A", "B"],
            services => services.AddBackgroundJobTypeDefaults("A", DeclaredDefaults.Tuned));

        // Act
        var threads = SchedulerOptions<ThreadPoolOptions>(provider).MaxConcurrency;
        var caps = QuartzRegistration.Build(["A", "B"], provider.GetRequiredService<JobTypePolicies>()).GroupCaps;

        // Assert — four for the declared type, one for the other, and the retention thread; the execution limits
        // read the same caps.
        threads.Should().Be(6);
        caps.Should().Equal(new Dictionary<string, int> { ["A"] = 4, ["B"] = 1 });
    }

    [Fact]
    public void AddJobsScheduler_ConfiguredMaxConcurrency_OverridesTheDeclaredCap()
    {
        // Arrange
        using var provider = SchedulerServices(
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:JobTypes:A:MaxConcurrency"] = "2" },
            ["A"],
            services => services.AddBackgroundJobTypeDefaults("A", DeclaredDefaults.Tuned));

        // Act
        var threads = SchedulerOptions<ThreadPoolOptions>(provider).MaxConcurrency;

        // Assert
        threads.Should().Be(3);
    }

    [Fact]
    public void AddJobsScheduler_Store_InsertsTriggersWithoutTheTriggerLock()
    {
        // Arrange
        using var provider = SchedulerServices(new Dictionary<string, string?>(), ["A"]);

        // Act
        var lockOnInsert = SchedulerOptions<AdoJobStoreOptions>(provider).LockOnInsert;

        // Assert
        lockOnInsert.Should().BeFalse();
    }

    // The module's own registration, with a handler registry of the given job types in place of the host's handlers.
    private static ServiceProvider SchedulerServices(
        Dictionary<string, string?> settings,
        string[] jobTypes,
        Action<IServiceCollection>? configureServices = null)
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
        configureServices?.Invoke(services);
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

    private static JobTypePolicies Configured(BackgroundJobsOptions options) =>
        new(Options.Create(options), DeclaredDefaults.None);

    private static JobTypePolicies Policies(BackgroundJobsOptions options, IServiceCollection declaredServices)
    {
        using var provider = declaredServices.BuildServiceProvider();
        return new(Options.Create(options), new JobTypeDefaults(provider.GetServices<BackgroundJobTypeDefaults>()));
    }

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
