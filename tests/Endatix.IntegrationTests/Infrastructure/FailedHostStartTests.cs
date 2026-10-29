namespace Endatix.IntegrationTests;

/// <summary>
/// A test host whose start fails is disposed before the failure reaches the test, every disposal runs, and the failure
/// the test sees is the start's own, whatever a disposal throws.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FailedHostStartTests
{
    [Fact]
    public async Task A_failed_start_runs_every_disposal_and_rethrows_its_own_failure_when_a_disposal_throws()
    {
        // Arrange
        InvalidOperationException startFailure = new("The host did not start.");
        var databaseDropped = false;

        // Act
        var thrown = await Record.ExceptionAsync(() => FailedHostStart.DisposeOnFailureAsync<object>(
            () => throw startFailure,
            () => throw new IOException("The host did not stop."),
            () =>
            {
                databaseDropped = true;
                return ValueTask.CompletedTask;
            }));

        // Assert
        Assert.Same(startFailure, thrown);
        Assert.True(databaseDropped, "A disposal after the one that threw did not run.");
        Assert.Contains("The host did not stop.", startFailure.Data.Values.Cast<string>().Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_start_of_a_host_built_in_a_constructor_rethrows_its_own_failure_when_its_disposal_throws()
    {
        // Arrange
        InvalidOperationException startFailure = new("The host did not start.");

        // Act
        var thrown = Record.Exception(() => FailedHostStart.DisposeOnFailure<object>(
            () => throw startFailure,
            () => throw new IOException("The host did not stop.")));

        // Assert
        Assert.Same(startFailure, thrown);
        Assert.Single(startFailure.Data.Values);
    }

    [Fact]
    public async Task A_start_that_succeeds_disposes_nothing()
    {
        // Arrange
        var disposed = false;

        // Act
        var started = await FailedHostStart.DisposeOnFailureAsync(
            () => Task.FromResult("host"),
            () =>
            {
                disposed = true;
                return ValueTask.CompletedTask;
            });

        // Assert
        Assert.Equal("host", started);
        Assert.False(disposed);
    }
}
