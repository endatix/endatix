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

public sealed class BackgroundJobExecutionTests
{
    private const long TenantA = 101;
    private const long JobId = 42;
    private const string JobType = "TenantProbe";

    public static TheoryData<string, string, int, int, string, bool> Outcomes => new()
    {
        { "success", nameof(AttemptEnd.Succeeded), 1, 3, nameof(AttemptRowWrite.Completed), false },
        { "failure result", nameof(AttemptEnd.ReturnedFailure), 1, 3, nameof(AttemptRowWrite.Failed), false },
        { "throw with attempts left", nameof(AttemptEnd.Threw), 2, 3, nameof(AttemptRowWrite.Retrying), true },
        { "throw on the last attempt", nameof(AttemptEnd.Threw), 3, 3, nameof(AttemptRowWrite.DeadLettered), false },
        { "throw past the budget after a recovery", nameof(AttemptEnd.Threw), 4, 3, nameof(AttemptRowWrite.DeadLettered), false },
        { "row canceled", nameof(AttemptEnd.Canceled), 1, 3, nameof(AttemptRowWrite.None), false },
        { "host shutdown", nameof(AttemptEnd.HostShutdown), 1, 3, nameof(AttemptRowWrite.None), false },
    };

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void DecideOutcome_EachHandlerOutcome_ReturnsExpectedRowAndQuartzAction(
        string caseId,
        string endName,
        int attemptCount,
        int maxAttempts,
        string expectedRowName,
        bool expectedRethrow)
    {
        // Arrange — the case id names the row of the outcome table under test.
        _ = caseId;
        var end = Enum.Parse<AttemptEnd>(endName);
        var expectedRow = Enum.Parse<AttemptRowWrite>(expectedRowName);

        // Act
        var decision = AttemptDecision.Decide(end, attemptCount, maxAttempts);

        // Assert
        decision.Should().Be(new AttemptDecision(expectedRow, expectedRethrow));
    }

    [Fact]
    public async Task ResolveContext_ClaimedRow_UsesRowTenantAndLeavesAmbientUnset()
    {
        // Arrange — the host serves no tenant, and the claimed row belongs to tenant A.
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(JobId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), false, Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(JobId, JobType, TenantA, "{}", 1, null, JobStatus.Processing));
        repository.TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert — the handler scopes its queries by the context's tenant, and nothing made tenant A ambient.
        observed.ContextTenantId.Should().Be(TenantA);
        observed.AmbientTenantId.Should().Be(0);
        await repository.Received(1).TryCompleteAsync(JobId, 1, Arg.Any<DateTime>(), CancellationToken.None);
    }

    [Fact]
    public void Resolve_ClaimedRow_CopiesRowFields()
    {
        // Arrange
        var resolver = new JobRowExecutionContextResolver();
        var claimed = new ClaimedJob(JobId, JobType, TenantA, """{"a":1}""", 3, "trace", JobStatus.Processing);

        // Act
        var context = resolver.Resolve(claimed);

        // Assert
        context.Should().Be(new BackgroundJobContext(JobId, JobType, TenantA, """{"a":1}""", 3));
    }

    private static ServiceProvider Services(IBackgroundJobStateRepository repository, ObservedRun observed)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repository);
        services.AddScoped<ITenantContext>(_ => new FixedTenantContext(0));
        services.AddSingleton(observed);
        services.AddScoped<IBackgroundJobHandler, TenantObservingHandler>();
        services.AddSingleton(provider => JobHandlerRegistry.Build(provider));
        services.AddSingleton<IJobExecutionContextResolver, JobRowExecutionContextResolver>();
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Options.Create(new BackgroundJobsOptions()));
        services.AddSingleton<JobsShutdownSignal>();
        services.AddSingleton(Substitute.For<IJobMetrics>());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        return services.BuildServiceProvider();
    }

    private static IJobExecutionContext FiringOf(long jobId)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(new JobDataMap { [BackgroundJobExecution.JobIdKey] = jobId.ToString() });
        context.Recovering.Returns(false);
        return context;
    }

    private sealed class ObservedRun
    {
        public long ContextTenantId { get; set; } = -1;

        public long AmbientTenantId { get; set; } = -1;
    }

    private sealed class TenantObservingHandler(ObservedRun observed, ITenantContext ambient) : IBackgroundJobHandler
    {
        public string JobType => BackgroundJobExecutionTests.JobType;

        public Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken)
        {
            observed.ContextTenantId = job.TenantId;
            observed.AmbientTenantId = ambient.TenantId;
            return Task.FromResult(Result.Success());
        }
    }
}
