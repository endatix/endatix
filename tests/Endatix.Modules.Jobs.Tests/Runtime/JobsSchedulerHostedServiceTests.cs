using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;
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

    private static JobsSchedulerHostedService CreateService() =>
        new(
            Substitute.For<IServiceProvider>(),
            new ConfigurationBuilder().Build(),
            JobHandlerRegistry.Build([]),
            Options.Create(new BackgroundJobsOptions()),
            NullLogger<JobsSchedulerHostedService>.Instance);
}
