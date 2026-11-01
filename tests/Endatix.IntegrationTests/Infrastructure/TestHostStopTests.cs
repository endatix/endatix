using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Endatix.IntegrationTests;

[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class TestHostStopTests(EndatixIntegrationWebHostFixture fixture)
{
    private const int Hosts = 3;

    [Fact]
    public async Task Disposing_a_test_host_stops_each_hosted_service_once()
    {
        // Arrange
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        var counters = Enumerable.Range(0, Hosts).Select(_ => new LifecycleCounter()).ToList();

        // Act — the double stop is a race, so a few hosts are started and disposed in turn.
        foreach (var counter in counters)
        {
            var host = root.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddSingleton<IHostedService>(counter)));
            _ = host.Services;
            await host.DisposeAsync();
        }

        // Assert
        counters.Should().AllSatisfy(counter =>
        {
            counter.Starts.Should().Be(1);
            counter.Stops.Should().Be(1);
        });
    }

    [Fact]
    public async Task Disposing_a_test_host_with_a_hosted_service_whose_StopAsync_throws_throws_that_failure_once()
    {
        // Arrange
        await using var root = new EndatixWebApplicationFactory(fixture.Database.ConnectionString, fixture.Database.Provider);
        var failure = new InvalidOperationException("The hosted service's StopAsync threw.");
        var host = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHostedService>(new FailingStop(failure))));
        _ = host.Services;

        // Act — disposing the root disposes the host again, since the host's own disposal did not finish.
        var stopFailure = await Record.ExceptionAsync(() => host.DisposeAsync().AsTask());
        var rootDisposal = await Record.ExceptionAsync(() => root.DisposeAsync().AsTask());

        // Assert
        stopFailure.Should().BeSameAs(failure);
        rootDisposal.Should().BeNull();
    }

    private sealed class FailingStop(Exception failure) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.FromException(failure);
    }

    private sealed class LifecycleCounter : IHostedService
    {
        private int _starts;
        private int _stops;

        public int Starts => Volatile.Read(ref _starts);

        public int Stops => Volatile.Read(ref _stops);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _starts);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _stops);
            return Task.CompletedTask;
        }
    }
}
