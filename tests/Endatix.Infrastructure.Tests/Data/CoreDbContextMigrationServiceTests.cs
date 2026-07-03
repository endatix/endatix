using Endatix.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Tests.Data;

public sealed class CoreDbContextMigrationServiceTests
{
    [Fact]
    public async Task MigrateAsync_WhenCoreContextsNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        // Act
        Func<Task> action = () => CoreDbContextMigrationService.MigrateCoreDbContextsAsync(
            provider,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AppDbContext is not registered in the service provider*");
    }
}
