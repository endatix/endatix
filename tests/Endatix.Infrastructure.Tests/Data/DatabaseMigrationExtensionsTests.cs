using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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
        Func<Task> action = () => provider.ApplyDbMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AppDbContext is not registered in the service provider*");
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
        // Arrange
        List<string> callOrder = [];
        ServiceCollection services = new();
        services.AddLogging();
        RegisterCoreDbContextMigrationStubs(services, callOrder);
        services.AddSingleton<IDbContextMigrationContributor>(
            new TrackingContributor("module", callOrder));

        await using var provider = services.BuildServiceProvider();

        // Act
        await provider.ApplyDbMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        callOrder.Should().Equal("core-app", "core-identity", "module");
    }

    private static void RegisterCoreDbContextMigrationStubs(IServiceCollection services, List<string> callOrder)
    {
        var appDbContext = Substitute.For<AppDbContext>();
        var appDatabase = Substitute.For<DatabaseFacade>(appDbContext);
        appDbContext.Database.Returns(appDatabase);
        appDatabase.GetMigrations().Returns(["20260101000000_TestApp"]);
        appDatabase.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("core-app"));

        var identityDbContext = Substitute.For<AppIdentityDbContext>();
        var identityDatabase = Substitute.For<DatabaseFacade>(identityDbContext);
        identityDbContext.Database.Returns(identityDatabase);
        identityDatabase.GetMigrations().Returns(["20260101000000_TestIdentity"]);
        identityDatabase.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add("core-identity"));

        services.AddSingleton(appDbContext);
        services.AddSingleton(identityDbContext);
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
