using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test host fails its first stop once, with the stop's own failure or a timeout naming the bound when the stop never
/// ends; later stops return at once, and the host is disposed only once its stop has ended.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("Lane", "Jobs")]
public sealed class StopOnceHostTests
{
    private static readonly TimeSpan StopBound = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task A_stop_that_never_ends_fails_once_later_stops_do_not_wait_and_dispose_leaves_the_host()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var inner = new FakeHost();
        var host = new StopOnceHost(inner, StopBound);

        // Act
        var firstStop = await Record.ExceptionAsync(() => host.StopAsync(ct));
        var laterStop = Stopwatch.StartNew();
        var secondStop = await Record.ExceptionAsync(() => host.StopAsync(ct));
        laterStop.Stop();
        host.Dispose();

        // Assert
        Assert.Equal("The test host did not stop within 2 s.", Assert.IsType<TimeoutException>(firstStop).Message);
        Assert.Null(secondStop);
        Assert.True(laterStop.Elapsed < StopBound / 2, $"the second stop waited {laterStop.Elapsed}");
        Assert.False(inner.Disposed);
    }

    [Fact]
    public async Task A_hosted_service_timeout_from_StopAsync_is_thrown_as_it_is_and_the_stopped_host_is_disposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var inner = new FakeHost();
        var host = new StopOnceHost(inner, StopBound);
        TimeoutException serviceTimeout = new("x");
        inner.StopsWith(serviceTimeout);

        // Act
        var stopFailure = await Record.ExceptionAsync(() => host.StopAsync(ct));
        host.Dispose();

        // Assert
        Assert.Same(serviceTimeout, stopFailure);
        Assert.True(inner.Disposed);
    }

    /// <summary>A host whose stop outcome arrives only when the test logs it, as the real host would.</summary>
    private sealed class FakeHost : IHost
    {
        private const int StoppedWithExceptionEventId = 5;

        private readonly ServiceProvider _services = new ServiceCollection()
            .AddSingleton<IHostApplicationLifetime>(new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance))
            .AddSingleton<HostStopOutcome>()
            .BuildServiceProvider();

        public bool Disposed { get; private set; }

        public IServiceProvider Services => _services;

        private HostStopOutcome Outcome => _services.GetRequiredService<HostStopOutcome>();

        public void StopsWith(Exception failure) =>
            Outcome.Log(
                LogLevel.Debug,
                new EventId(StoppedWithExceptionEventId),
                "Hosting shutdown exception",
                failure,
                (state, _) => state);

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
            Disposed = true;
            _services.Dispose();
        }
    }
}
