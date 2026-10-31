using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test host whose stop never ends fails its first stop with a timeout that names the bound, returns from every later
/// stop at once, and is not disposed under hosted services that may still be running.
/// </summary>
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class StopOnceHostTests
{
    private static readonly TimeSpan StopBound = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task A_stop_that_times_out_fails_once_and_later_stops_neither_wait_again_nor_dispose_the_host()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var inner = new NeverStoppingHost();
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

    /// <summary>A host whose stop outcome never arrives, as when a hosted service ignores the stop.</summary>
    private sealed class NeverStoppingHost : IHost
    {
        private readonly ServiceProvider _services = new ServiceCollection()
            .AddSingleton<IHostApplicationLifetime>(new ApplicationLifetime(NullLogger<ApplicationLifetime>.Instance))
            .AddSingleton<HostStopOutcome>()
            .BuildServiceProvider();

        public bool Disposed { get; private set; }

        public IServiceProvider Services => _services;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
            Disposed = true;
            _services.Dispose();
        }
    }
}
