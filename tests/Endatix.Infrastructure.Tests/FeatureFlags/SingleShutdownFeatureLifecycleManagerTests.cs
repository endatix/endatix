using System.Threading.Channels;
using Endatix.Infrastructure.FeatureFlags;
using Endatix.Infrastructure.Features.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenFeature.Hosting;

namespace Endatix.Infrastructure.Tests.FeatureFlags;

public sealed class SingleShutdownFeatureLifecycleManagerTests
{
    public SingleShutdownFeatureLifecycleManagerTests()
    {
        // The guard is process-wide by design, so each test has to start from a clean slate.
        SingleShutdownFeatureLifecycleManager.ResetForTests();
    }

    [Fact]
    public async Task ShutdownAsync_FirstCall_ShutsDownTheInnerManager()
    {
        // Arrange
        var inner = new RecordingFeatureLifecycleManager();
        var manager = new SingleShutdownFeatureLifecycleManager(inner);

        // Act
        await manager.ShutdownAsync(TestContext.Current.CancellationToken);

        // Assert
        inner.ShutdownCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ShutdownAsync_CalledTwice_ReachesTheInnerManagerOnce()
    {
        // Arrange
        var inner = new RecordingFeatureLifecycleManager();
        var manager = new SingleShutdownFeatureLifecycleManager(inner);

        // Act
        await manager.ShutdownAsync(TestContext.Current.CancellationToken);
        await manager.ShutdownAsync(TestContext.Current.CancellationToken);

        // Assert
        inner.ShutdownCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ShutdownAsync_SecondHostInTheSameProcess_DoesNotThrow()
    {
        // Arrange
        // OpenFeature.Api is a process-wide singleton whose event channel cannot be reopened, so a
        // real second shutdown throws ChannelClosedException. Two decorator instances stand in for
        // the two hosts, each with its own inner manager, sharing the same static guard.
        var firstHost = new SingleShutdownFeatureLifecycleManager(new RecordingFeatureLifecycleManager());
        var secondInner = new RecordingFeatureLifecycleManager(throwOnShutdown: true);
        var secondHost = new SingleShutdownFeatureLifecycleManager(secondInner);

        await firstHost.ShutdownAsync(TestContext.Current.CancellationToken);

        // Act
        var secondShutdown = async () => await secondHost.ShutdownAsync(TestContext.Current.CancellationToken);

        // Assert
        await secondShutdown.Should().NotThrowAsync();
        secondInner.ShutdownCallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureInitializedAsync_EveryCall_ReachesTheInnerManager()
    {
        // Arrange
        // Initialization is per host and must never be suppressed — only shutdown is one-way.
        var inner = new RecordingFeatureLifecycleManager();
        var manager = new SingleShutdownFeatureLifecycleManager(inner);

        // Act
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);
        await manager.EnsureInitializedAsync(TestContext.Current.CancellationToken);

        // Assert
        inner.InitializeCallCount.Should().Be(2);
    }

    [Fact]
    public void AddEndatixOpenFeature_DecoratesTheLifecycleManager()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddEndatixOpenFeature();

        // Assert
        var manager = services.BuildServiceProvider().GetRequiredService<IFeatureLifecycleManager>();
        manager.Should().BeOfType<SingleShutdownFeatureLifecycleManager>();
    }

    [Fact]
    public void AddEndatixOpenFeature_CalledTwice_RegistersASingleDecorator()
    {
        // Arrange
        var services = CreateServices();

        // Act
        services.AddEndatixOpenFeature();
        services.AddEndatixOpenFeature();

        // Assert
        services.Count(service => service.ServiceType == typeof(IFeatureLifecycleManager))
            .Should().Be(1);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }

    private sealed class RecordingFeatureLifecycleManager(bool throwOnShutdown = false)
        : IFeatureLifecycleManager
    {
        public int InitializeCallCount { get; private set; }

        public int ShutdownCallCount { get; private set; }

        public ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (throwOnShutdown)
            {
                throw new ChannelClosedException();
            }

            ShutdownCallCount++;
            return ValueTask.CompletedTask;
        }
    }
}
