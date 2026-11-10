namespace Endatix.IntegrationTests;

/// <summary>
/// What a failed creation built is disposed before the failure reaches the test, every disposal runs, and the failure the
/// test sees is the creation's own, whatever a disposal throws.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FailedCreationTests
{
    // Named so that a reader of the test output sees these failures were produced on purpose.
    private const string DeliberateCreationFailure = "Deliberate creation failure (FailedCreationTests)";
    private const string DeliberateDisposalFailure = "Deliberate disposal failure (FailedCreationTests)";

    [Fact]
    public async Task A_failed_creation_runs_every_disposal_and_rethrows_its_own_failure_when_a_disposal_throws()
    {
        // Arrange
        InvalidOperationException creationFailure = new(DeliberateCreationFailure);
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedCreation.DisposeOnFailureAsync<object>(
            () => throw creationFailure,
            () => throw new IOException(DeliberateDisposalFailure),
            () => Disposing(disposed, "database")));

        // Assert
        Assert.Same(creationFailure, thrown);

        // The creation's own frame is still on the trace: the failure was rethrown, not thrown anew.
        Assert.Contains(
            nameof(A_failed_creation_runs_every_disposal_and_rethrows_its_own_failure_when_a_disposal_throws),
            creationFailure.StackTrace,
            StringComparison.Ordinal);
        Assert.Equal(["database"], disposed);
        var warning = Assert.Single(TestContext.Current.Warnings ?? []);
        Assert.StartsWith(TestContext.Current.Test!.TestDisplayName, warning, StringComparison.Ordinal);
        Assert.Contains(DeliberateDisposalFailure, warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_creation_that_fails_after_an_await_runs_every_disposal_in_order_and_rethrows_its_own_failure()
    {
        // Arrange
        InvalidOperationException creationFailure = new(DeliberateCreationFailure);
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedCreation.DisposeOnFailureAsync<object>(
            async () =>
            {
                await Task.Yield();
                throw creationFailure;
            },
            async () =>
            {
                // Finishes later than the database's disposal would, so disposals run at once record the database first.
                await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
                disposed.Add("host");
            },
            () => Disposing(disposed, "database")));

        // Assert
        Assert.Same(creationFailure, thrown);
        Assert.Equal(["host", "database"], disposed);
    }

    [Fact]
    public async Task A_cancelled_creation_runs_every_disposal_and_rethrows_its_cancellation()
    {
        // Arrange
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedCreation.DisposeOnFailureAsync(
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
    public async Task A_creation_that_succeeds_disposes_nothing()
    {
        // Arrange
        List<string> disposed = [];

        // Act
        var created = await FailedCreation.DisposeOnFailureAsync(
            () => Task.FromResult("host"),
            () => Disposing(disposed, "host"));

        // Assert
        Assert.Equal("host", created);
        Assert.Empty(disposed);
    }

    private static ValueTask Disposing(List<string> disposed, string name)
    {
        disposed.Add(name);
        return ValueTask.CompletedTask;
    }
}
