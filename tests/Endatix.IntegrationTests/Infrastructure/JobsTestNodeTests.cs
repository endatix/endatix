using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

/// <summary>
/// A scheduler node's teardown fails its test when the node does not stop cleanly and disposes the node either way; a
/// node whose start fails is stopped and disposed, and the failure the test sees is the start's own.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsTestNodeTests(DbIntegrationFixture fixture)
{
    // Named so that a reader of the test output sees these failures were produced on purpose.
    private const string DeliberateStartFailure = "Deliberate start failure (JobsTestNodeTests)";
    private const string DeliberateStopFailure = "Deliberate stop failure (JobsTestNodeTests)";

    [Fact]
    public async Task A_node_with_a_service_that_throws_while_it_stops_fails_its_teardown_and_is_still_disposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var service = new RecordingService(DeliberateStopFailure);
        var node = JobsTestNode.Create(database, configureServices: services => services.AddHostedService(_ => service));
        await node.StartAsync(ct);

        // Act
        var thrown = await Record.ExceptionAsync(() => node.DisposeAsync().AsTask());

        // Assert
        Assert.Equal(DeliberateStopFailure, Assert.IsType<InvalidOperationException>(thrown).Message);
        Assert.True(service.Disposed);
    }

    [Fact]
    public async Task A_node_whose_start_fails_is_stopped_and_disposed_before_the_start_failure_reaches_the_test()
    {
        // Arrange — a service that starts, then one whose start fails.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var started = new RecordingService();
        var node = JobsTestNode.Create(database, configureServices: services =>
        {
            services.AddHostedService(_ => started);
            services.AddHostedService<FailingStartService>();
        });

        // Act
        var thrown = await Record.ExceptionAsync(() => node.StartedAsync(ct));

        // Assert
        Assert.Equal(DeliberateStartFailure, Assert.IsType<InvalidOperationException>(thrown).Message);
        Assert.True(started.Stopped);
        Assert.True(started.Disposed);
    }

    [Fact]
    public async Task A_node_whose_start_failed_reports_a_failing_stop_as_a_warning_when_disposed()
    {
        // Arrange — a service that starts and throws while it stops, then one whose start fails.
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var started = new RecordingService(DeliberateStopFailure);
        var node = JobsTestNode.Create(database, configureServices: services =>
        {
            services.AddHostedService(_ => started);
            services.AddHostedService<FailingStartService>();
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => node.StartAsync(ct));

        // Act
        var thrown = await Record.ExceptionAsync(() => node.DisposeAsync().AsTask());

        // Assert
        Assert.Null(thrown);
        Assert.True(started.Disposed);
        var warning = Assert.Single(TestContext.Current.Warnings ?? []);
        Assert.Contains("the stop after the failed start failed:", warning, StringComparison.Ordinal);
        Assert.Contains(DeliberateStopFailure, warning, StringComparison.Ordinal);
    }

    // Records its stop and its disposal, and throws stopFailure, when it has one, from its stop.
    private sealed class RecordingService(string? stopFailure = null) : IHostedService, IDisposable
    {
        public bool Stopped { get; private set; }

        public bool Disposed { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return stopFailure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(stopFailure));
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FailingStartService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException(DeliberateStartFailure));

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
