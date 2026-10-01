using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class CancellationWatcherTests
{
    private const long JobId = 42;
    private const int ClaimedAttempt = 1;

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Token_RowCanceled_TripsAsCancellation()
    {
        // Arrange
        using var services = ServicesReading(new JobAttemptState(JobStatus.Canceled, ClaimedAttempt));

        // Act
        await using var watcher = Start(services);
        var tripped = await TrippedAsync(watcher);

        // Assert
        tripped.Should().BeTrue();
        watcher.SawCancellation.Should().BeTrue();
        watcher.SawSupersession.Should().BeFalse();
    }

    [Fact]
    public async Task Token_RowTakenOverByLaterAttempt_TripsAsSupersession()
    {
        // Arrange — another node recovered the job and took attempt 2 while this attempt still runs.
        using var services = ServicesReading(new JobAttemptState(JobStatus.Processing, ClaimedAttempt + 1));

        // Act
        await using var watcher = Start(services);
        var tripped = await TrippedAsync(watcher);

        // Assert
        tripped.Should().BeTrue();
        watcher.SawSupersession.Should().BeTrue();
        watcher.SawCancellation.Should().BeFalse();
    }

    [Theory]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Retrying)]
    public async Task Token_RowNoLongerProcessingForThisAttempt_TripsAsSupersession(JobStatus status)
    {
        // Arrange
        using var services = ServicesReading(new JobAttemptState(status, ClaimedAttempt));

        // Act
        await using var watcher = Start(services);
        var tripped = await TrippedAsync(watcher);

        // Assert
        tripped.Should().BeTrue();
        watcher.SawSupersession.Should().BeTrue();
    }

    [Fact]
    public async Task Token_RowStillThisAttempt_StaysUntripped()
    {
        // Arrange
        using var services = ServicesReading(new JobAttemptState(JobStatus.Processing, ClaimedAttempt));
        var repository = services.GetRequiredService<IBackgroundJobStateRepository>();

        // Act
        await using var watcher = Start(services);
        await WaitForReadsAsync(repository, 3);

        // Assert
        watcher.Token.IsCancellationRequested.Should().BeFalse();
        watcher.SawCancellation.Should().BeFalse();
        watcher.SawSupersession.Should().BeFalse();
    }

    private static ServiceProvider ServicesReading(JobAttemptState state)
    {
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>()).Returns(state);
        return new ServiceCollection().AddScoped(_ => repository).BuildServiceProvider();
    }

    private static CancellationWatcher Start(ServiceProvider services) =>
        CancellationWatcher.Start(
            services.GetRequiredService<IServiceScopeFactory>(),
            new WatchedAttempt(new AttemptRef(JobId, ClaimedAttempt), Interval),
            NullLogger.Instance);

    private static async Task<bool> TrippedAsync(CancellationWatcher watcher)
    {
        try
        {
            await Task.Delay(Patience, watcher.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    private static async Task WaitForReadsAsync(IBackgroundJobStateRepository repository, int reads)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (repository.ReceivedCalls().Count() < reads && DateTime.UtcNow < deadline)
        {
            await Task.Delay(Interval);
        }
    }
}
