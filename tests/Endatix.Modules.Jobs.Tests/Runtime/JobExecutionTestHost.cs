using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>The job wrapper wired up for unit tests: a probe handler, a substitute row store and firings.</summary>
internal static class JobExecutionTestHost
{
    public const long TenantA = 101;
    public const long JobId = 42;
    public const string ProbeJobType = "TenantProbe";

    public static ServiceProvider Services(
        IBackgroundJobStateRepository repository,
        ObservedRun observed,
        BackgroundJobsOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repository);
        services.AddScoped<ITenantContext>(_ => new FixedTenantContext(0));
        services.AddSingleton(observed);
        services.AddScoped<IBackgroundJobHandler, TenantObservingHandler>();
        services.AddSingleton(provider => JobHandlerRegistry.Build(provider));
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Options.Create(options ?? new BackgroundJobsOptions()));
        services.AddSingleton(Substitute.For<IJobMetrics>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddJobExecution();
        return services.BuildServiceProvider();
    }

    // An argument matcher, so it is only called inside a call being configured or checked.
    public static JobClaim ClaimOfJob(bool recovering) =>
        Arg.Is<JobClaim>(claim => claim.JobId == JobId && claim.Recovering == recovering);

    public static IJobExecutionContext FiringOf(long jobId, string jobType = ProbeJobType)
    {
        var trigger = TriggerBuilder.Create()
            .WithIdentity(jobId.ToString(), jobType)
            .ForJob(jobType, "endatix")
            .WithExecutionGroup(jobType)
            .UsingJobData(BackgroundJobExecution.JobIdKey, jobId.ToString())
            .StartNow()
            .Build();
        var context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(new JobDataMap { [BackgroundJobExecution.JobIdKey] = jobId.ToString() });
        context.Recovering.Returns(false);
        context.Trigger.Returns(trigger);
        context.JobDetail.Returns(QuartzRegistration.DurableJobFor(jobType));
        context.Scheduler.Returns(Substitute.For<IScheduler>());
        return context;
    }

    public static IBackgroundJobStateRepository ClaimingRepository(int attempt)
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, ProbeJobType, TenantA, "{}", attempt, null, JobStatus.Processing));
        return repository;
    }

    // A firing of the job's own trigger, as enqueueing schedules it.
    public static IJobExecutionContext JobTriggerFiringOf(long jobId)
    {
        var context = FiringOf(jobId);
        context.Trigger.Returns(QuartzRegistration.TriggerFor(new JobTriggerSpec(jobId, ProbeJobType)));
        context.Scheduler.Returns(Substitute.For<IScheduler>());
        return context;
    }

    // A firing of the job's trigger as an earlier version stored it, with a scheduler retry policy.
    public static IJobExecutionContext LegacyJobTriggerFiringOf(long jobId)
    {
        var context = FiringOf(jobId);
        context.Trigger.Returns(TriggerBuilder.Create()
            .WithIdentity(jobId.ToString(), ProbeJobType)
            .ForJob(QuartzRegistration.JobKeyFor(ProbeJobType))
            .WithExecutionGroup(ProbeJobType)
            .UsingJobData(BackgroundJobExecution.JobIdKey, jobId.ToString())
            .WithRetryPolicy(RetryPolicy.Exponential(2, TimeSpan.FromSeconds(30), 2.0, TimeSpan.FromSeconds(900)))
            .StartNow()
            .Build());
        return context;
    }

    public static IJobExecutionContext RecoveryOf(long jobId)
    {
        var context = FiringOf(jobId);
        context.Recovering.Returns(true);

        // Shaped like the trigger Quartz creates to recover a dead node's firing: its own key and no retry policy.
        context.Trigger.Returns(TriggerBuilder.Create()
            .WithIdentity("recover_node-a_1", SchedulerConstants.DefaultRecoveryGroup)
            .ForJob(QuartzRegistration.JobKeyFor(ProbeJobType))
            .UsingJobData(BackgroundJobExecution.JobIdKey, jobId.ToString())
            .StartNow()
            .Build());
        context.Scheduler.Returns(Substitute.For<IScheduler>());
        return context;
    }
}

internal sealed class ObservedRun
{
    public long ContextTenantId { get; set; } = -1;

    public long AmbientTenantId { get; set; } = -1;

    public bool RaiseShutdown { get; init; }

    public bool ThrowAfterShutdown { get; init; }

    public bool Throw { get; init; }

    public bool WaitForCancellation { get; init; }

    public bool HandlerTokenCancelled { get; set; }
}

internal sealed class TenantObservingHandler(ObservedRun observed, ITenantContext ambient, JobsShutdownSignal shutdown)
    : IBackgroundJobHandler
{
    public string JobType => JobExecutionTestHost.ProbeJobType;

    public async Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
    {
        observed.ContextTenantId = job.TenantId;
        observed.AmbientTenantId = ambient.TenantId;
        if (observed.RaiseShutdown)
        {
            shutdown.Raise();
        }

        if (observed.Throw)
        {
            throw new InvalidOperationException("Transient failure.");
        }

        if (observed.ThrowAfterShutdown)
        {
            throw new OperationCanceledException(shutdown.Token);
        }

        if (observed.WaitForCancellation)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                observed.HandlerTokenCancelled = true;
                throw;
            }
        }

        return Result.Success();
    }
}
