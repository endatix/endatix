using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>An outcome write a read-only database refuses: carried, and said so, unless the host is stopping.</summary>
public sealed partial class BackgroundJobExecutionTests
{
    [Fact]
    public async Task RecordUnsavedAsync_ReadOnlyDatabase_WarnsThatTheOutcomeIsCarried()
    {
        // Arrange
        var warnings = Substitute.For<ILogger<ReadOnlyDatabaseWarning>>();
        await using var provider = Services(ReadOnlyCompletions(), new ObservedRun());
        var recorder = ActivatorUtilities.CreateInstance<JobOutcomeRecorder>(
            provider, new ReadOnlyDatabaseWarning(Substitute.For<IDateTimeProvider>(), warnings));

        // Act
        var written = await recorder.RecordUnsavedAsync(new ReclaimableJob(JobId, ProbeJobType), Carried(AttemptRowWrite.Completed));

        // Assert
        written.Should().Be(OutcomeWrite.Unwritable);
        WarningsOf(warnings).Should().Be(1);
    }

    [Fact]
    public async Task RecordUnsavedAsync_ReadOnlyDatabaseWhileTheHostStops_SaysNothingOfCarryingIt()
    {
        // Arrange — a stopping host carries no outcome on.
        var warnings = Substitute.For<ILogger<ReadOnlyDatabaseWarning>>();
        await using var provider = Services(ReadOnlyCompletions(), new ObservedRun());
        var recorder = ActivatorUtilities.CreateInstance<JobOutcomeRecorder>(
            provider, new ReadOnlyDatabaseWarning(Substitute.For<IDateTimeProvider>(), warnings));
        provider.GetRequiredService<JobsShutdownSignal>().Raise();

        // Act
        await recorder.RecordUnsavedAsync(new ReclaimableJob(JobId, ProbeJobType), Carried(AttemptRowWrite.Completed));

        // Assert
        WarningsOf(warnings).Should().Be(0);
    }

    // A completion write a read-only PostgreSQL database refuses on every try, while the row still reads.
    private static IBackgroundJobStateRepository ReadOnlyCompletions()
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, MaxAttempts), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new PostgresException(
                "cannot execute UPDATE in a read-only transaction", "ERROR", "ERROR", PostgresErrorCodes.ReadOnlySqlTransaction));
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns(new JobAttemptState(JobStatus.Processing, MaxAttempts));
        return repository;
    }

    private static int WarningsOf(ILogger<ReadOnlyDatabaseWarning> logger) =>
        logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log)
            && (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
}
