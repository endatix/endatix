using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Data;

public class ModuleDbContextExtensionsTests
{
    [Fact]
    public void AddModuleDbContext_WithNullConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        var configure = (ModuleDbContextOptions options) =>
        {
            options.MigrationsAssembly = "TestAssembly";
            options.PostgreSqlMigrationsNamespace = "Test.Migrations.PostgreSql";
            options.SqlServerMigrationsNamespace = "Test.Migrations.SqlServer";
        };

        // Act
        var action = () => services.AddModuleDbContext<TestDbContext>(configuration, configure);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("DefaultConnection");
    }

    [Fact]
    public void AddModuleDbContext_WithProviderOptionsRegistered_AppliesThemToTheContext()
    {
        // Arrange
        RecordingProviderOptions providerOptions = new();
        ServiceCollection services = new();
        services.AddSingleton<IModuleDbContextProviderOptions>(providerOptions);
        services.AddModuleDbContext<TestDbContext>(PostgreSqlConfiguration(), ConfigureTestModule);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        using var scope = serviceProvider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        // Assert
        providerOptions.AppliedCount.Should().Be(1);
    }

    [Fact]
    public void AddModuleDbContext_WithoutProviderOptions_CreatesTheContext()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddModuleDbContext<TestDbContext>(PostgreSqlConfiguration(), ConfigureTestModule);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        // Assert
        context.Should().NotBeNull();
    }

    private static IConfiguration PostgreSqlConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "PostgreSql",
            })
            .Build();

    private static void ConfigureTestModule(ModuleDbContextOptions options)
    {
        options.Schema = "test";
        options.MigrationsAssembly = "TestAssembly";
        options.PostgreSqlMigrationsNamespace = "Test.Migrations.PostgreSql";
        options.SqlServerMigrationsNamespace = "Test.Migrations.SqlServer";
    }

    private sealed class RecordingProviderOptions : IModuleDbContextProviderOptions
    {
        public int AppliedCount { get; private set; }

        public void Apply(DbContextOptionsBuilder optionsBuilder) => AppliedCount++;
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }
}
