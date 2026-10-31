namespace Endatix.IntegrationTests;

/// <summary>
/// A test host whose start fails is disposed before the failure reaches the test, every disposal runs, and the failure
/// the test sees is the start's own, whatever a disposal throws.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FailedHostStartTests
{
    // Named so that a reader of the test output sees these failures were produced on purpose.
    private const string DeliberateStartFailure = "Deliberate start failure (FailedHostStartTests)";
    private const string DeliberateDisposalFailure = "Deliberate disposal failure (FailedHostStartTests)";

    [Fact]
    public async Task A_failed_start_runs_every_disposal_and_rethrows_its_own_failure_when_a_disposal_throws()
    {
        // Arrange
        InvalidOperationException startFailure = new(DeliberateStartFailure);
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedHostStart.DisposeOnFailureAsync<object>(
            () => throw startFailure,
            () => throw new IOException(DeliberateDisposalFailure),
            () => Disposing(disposed, "database")));

        // Assert
        Assert.Same(startFailure, thrown);

        // The start's own frame is still on the trace: the failure was rethrown, not thrown anew.
        Assert.Contains(
            nameof(A_failed_start_runs_every_disposal_and_rethrows_its_own_failure_when_a_disposal_throws),
            startFailure.StackTrace,
            StringComparison.Ordinal);
        Assert.Equal(["database"], disposed);
        var warning = Assert.Single(TestContext.Current.Warnings ?? []);
        Assert.StartsWith(TestContext.Current.Test!.TestDisplayName, warning, StringComparison.Ordinal);
        Assert.Contains(DeliberateDisposalFailure, warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_start_that_fails_after_an_await_runs_every_disposal_in_order_and_rethrows_its_own_failure()
    {
        // Arrange
        InvalidOperationException startFailure = new(DeliberateStartFailure);
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedHostStart.DisposeOnFailureAsync<object>(
            async () =>
            {
                await Task.Yield();
                throw startFailure;
            },
            async () =>
            {
                // Finishes later than the database's disposal would, so disposals run at once record the database first.
                await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
                disposed.Add("host");
            },
            () => Disposing(disposed, "database")));

        // Assert
        Assert.Same(startFailure, thrown);
        Assert.Equal(["host", "database"], disposed);
    }

    [Fact]
    public async Task A_cancelled_start_runs_every_disposal_and_rethrows_its_cancellation()
    {
        // Arrange
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedHostStart.DisposeOnFailureAsync(
            async () =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
                return "host";
            },
            () => Disposing(disposed, "host"),
            () => Disposing(disposed, "database")));

        // Assert
        Assert.Equal(cancellation.Token, Assert.IsType<TaskCanceledException>(thrown).CancellationToken);
        Assert.Equal(["host", "database"], disposed);
    }

    [Fact]
    public async Task A_start_that_succeeds_disposes_nothing()
    {
        // Arrange
        List<string> disposed = [];

        // Act
        var started = await FailedHostStart.DisposeOnFailureAsync(
            () => Task.FromResult("host"),
            () => Disposing(disposed, "host"));

        // Assert
        Assert.Equal("host", started);
        Assert.Empty(disposed);
    }

    private static ValueTask Disposing(List<string> disposed, string name)
    {
        disposed.Add(name);
        return ValueTask.CompletedTask;
    }
}
