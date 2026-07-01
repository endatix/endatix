using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Data;

public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task ApplyDbMigrationsAsync_WithNoContributors_DoesNotThrowWhenCoreContextsNotRegistered()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        // Act
        Func<Task> action = () => provider.ApplyDbMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ApplyDbMigrationsAsync_IteratesAllRegisteredContributors()
    {
        // Arrange
        List<string> callOrder = [];
        ServiceCollection services = new();
        services.AddLogging();

        services.AddSingleton<IDbContextMigrationContributor>(new TrackingContributor("first", callOrder));
        services.AddSingleton<IDbContextMigrationContributor>(new TrackingContributor("second", callOrder));

        await using var provider = services.BuildServiceProvider();

        // Act
        await provider.ApplyDbMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        callOrder.Should().Equal("first", "second");
    }

    [Fact]
    public async Task ApplyDbMigrationsAsync_RunsContributorsAfterCorePhaseCompletes()
    {
        // Arrange — core phase runs first (no-op when contexts are not registered), then contributors.
        List<string> callOrder = [];
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IDbContextMigrationContributor>(
            new TrackingContributor("module", callOrder));

        await using var provider = services.BuildServiceProvider();

        // Act
        await provider.ApplyDbMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        callOrder.Should().Equal("module");
    }

    private sealed class TrackingContributor(string name, List<string> callOrder) : IDbContextMigrationContributor
    {
        public Task MigrateAsync(
            IServiceProvider scopedProvider,
            Microsoft.Extensions.Logging.ILogger logger,
            CancellationToken cancellationToken = default)
        {
            callOrder.Add(name);
            return Task.CompletedTask;
        }
    }
}
