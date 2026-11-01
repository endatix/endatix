namespace Endatix.IntegrationTests;

/// <summary>
/// A host on a database of its own is disposed first and its database is dropped whatever the host's disposal did; the
/// failure the test sees is the host's when the host's disposal fails, and the drop's otherwise.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class DisposalFailuresTests
{
    // Named so that a reader of the test output sees these failures were produced on purpose.
    private const string DeliberateHostFailure = "Deliberate host disposal failure (DisposalFailuresTests)";
    private const string DeliberateDropFailure = "Deliberate database drop failure (DisposalFailuresTests)";

    [Fact]
    public async Task A_host_whose_disposal_fails_still_has_its_database_dropped_and_its_own_failure_thrown()
    {
        // Arrange
        InvalidOperationException hostFailure = new(DeliberateHostFailure);
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => DisposalFailures.DisposeHostThenDatabaseAsync(
            new Disposable(() => throw hostFailure),
            new Disposable(() => Disposing(disposed, "database"))).AsTask());

        // Assert
        Assert.Same(hostFailure, thrown);
        Assert.Equal(["database"], disposed);
        Assert.Empty(TestContext.Current.Warnings ?? []);
    }

    [Fact]
    public async Task When_the_host_and_the_drop_both_fail_the_host_failure_is_thrown_and_the_drop_reported()
    {
        // Arrange
        InvalidOperationException hostFailure = new(DeliberateHostFailure);

        // Act
        var thrown = await Record.ExceptionAsync(() => DisposalFailures.DisposeHostThenDatabaseAsync(
            new Disposable(() => throw hostFailure),
            new Disposable(() => throw new IOException(DeliberateDropFailure))).AsTask());

        // Assert
        Assert.Same(hostFailure, thrown);
        var warning = Assert.Single(TestContext.Current.Warnings ?? []);
        Assert.StartsWith(TestContext.Current.Test!.TestDisplayName, warning, StringComparison.Ordinal);
        Assert.Contains("the database drop after a failed host disposal failed:", warning, StringComparison.Ordinal);
        Assert.Contains(DeliberateDropFailure, warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_drop_that_fails_after_the_host_was_disposed_throws_its_own_failure()
    {
        // Arrange
        IOException dropFailure = new(DeliberateDropFailure);
        List<string> disposed = [];

        // Act
        var thrown = await Record.ExceptionAsync(() => DisposalFailures.DisposeHostThenDatabaseAsync(
            new Disposable(() => Disposing(disposed, "host")),
            new Disposable(() => throw dropFailure)).AsTask());

        // Assert
        Assert.Same(dropFailure, thrown);
        Assert.Equal(["host"], disposed);
    }

    private static ValueTask Disposing(List<string> disposed, string name)
    {
        disposed.Add(name);
        return ValueTask.CompletedTask;
    }

    private sealed class Disposable(Func<ValueTask> dispose) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => dispose();
    }
}
