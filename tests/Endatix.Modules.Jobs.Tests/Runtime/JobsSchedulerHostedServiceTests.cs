using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class JobsSchedulerHostedServiceTests
{
    [Fact]
    public void Dispose_WithoutStop_DoesNotThrow()
    {
        // Arrange — the host disposes a service it never stopped when another hosted service fails to start.
        var service = CreateService();

        // Act
        var dispose = service.Dispose;

        // Assert
        dispose.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_AfterStop_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        await service.StopAsync(CancellationToken.None);

        // Act
        var dispose = service.Dispose;

        // Assert
        dispose.Should().NotThrow();
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        service.Dispose();

        // Act
        var disposeAgain = service.Dispose;

        // Assert
        disposeAgain.Should().NotThrow();
    }

    private static JobsSchedulerHostedService CreateService()
    {
        var database = new JobStoreDatabase("Host=localhost", isPostgreSql: true, locksOnInsert: false);
        var options = Options.Create(new BackgroundJobsOptions());
        return new(
            new ServiceCollection().AddSingleton(Substitute.For<IHostApplicationLifetime>()).BuildServiceProvider(),
            database,
            JobHandlerRegistry.Build([]),
            options,
            new JobsShutdownSignal(),
            NullLogger<JobsSchedulerHostedService>.Instance);
    }
}
