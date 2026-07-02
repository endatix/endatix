using Endatix.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Data;

public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task ApplyDbMigrationsAsync_WhenCoreContextsNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        // Act
        Func<Task> action = () => provider.ApplyDbMigrationsAsync(NullLogger.Instance, TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AppDbContext is not registered in the service provider*");
    }
}
