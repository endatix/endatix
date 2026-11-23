using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>A system job that reaches a handler written for tenant work.</summary>
public sealed partial class BackgroundJobExecutionTests
{
    [Fact]
    public async Task Execute_SystemJobReachesTenantHandler_FailsOnceAsWrongScopeWithoutRetry()
    {
        // Arrange — the claimed row is a system job; the probe handler reads job.TenantId.
        var observed = new ObservedRun();
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository
            .TryClaimAsync(ClaimOfJob(recovering: false), Arg.Any<CancellationToken>())
            .Returns(new ClaimedJob(
                JobId, ProbeJobType, JobScopeStorage.ToStorage(JobScope.System), "{}", 1, null, JobStatus.Processing));
        repository.TryFailAsync(new AttemptRef(JobId, 1), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, observed);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);

        // Act
        await execution.Execute(FiringOf(JobId), TestContext.Current.CancellationToken);

        // Assert — no retry can change a job's scope, so the first attempt is the last.
        await repository.Received(1).TryFailAsync(
            new AttemptRef(JobId, 1),
            Arg.Is<AttemptFailure>(failure => failure.ErrorMessage == JobScopeMismatchException.WrongScope),
            Arg.Any<CancellationToken>());
        await repository.DidNotReceiveWithAnyArgs().RecordFailedAttemptAsync(default, default, Arg.Any<CancellationToken>());
    }
}
