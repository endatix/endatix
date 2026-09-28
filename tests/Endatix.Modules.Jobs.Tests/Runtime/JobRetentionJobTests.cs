using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobRetentionJobTests
{
    [Fact]
    public async Task Execute_HostShuttingDown_DeletesNothing()
    {
        // Arrange — the scheduler never cancels its own token at shutdown, so the host's signal must stop the run.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        var services = new ServiceCollection().AddScoped(_ => repository).BuildServiceProvider();
        var shutdown = new JobsShutdownSignal();
        shutdown.Raise();
        var job = new JobRetentionJob(
            services.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IDateTimeProvider>(),
            Options.Create(new BackgroundJobsOptions()),
            shutdown,
            NullLogger<JobRetentionJob>.Instance);

        // Act
        var run = async () => await job.Execute(Substitute.For<IJobExecutionContext>(), TestContext.Current.CancellationToken);

        // Assert
        await run.Should().ThrowAsync<OperationCanceledException>();
        await repository.DidNotReceiveWithAnyArgs().DeleteExpiredAsync(default, default, default);
    }
}
