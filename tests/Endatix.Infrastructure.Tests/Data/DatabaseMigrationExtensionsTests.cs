using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Data;

public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task ApplyDbMigrationsAsync_IteratesAllRegisteredContributors()
    {
        // Arrange
        var callOrder = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IDbContextMigrationContributor>(new TrackingContributor("first", callOrder));
        services.AddSingleton<IDbContextMigrationContributor>(new TrackingContributor("second", callOrder));

        var provider = services.BuildServiceProvider();

        // Act
        await provider.ApplyDbMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        callOrder.Should().Equal("first", "second");
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
