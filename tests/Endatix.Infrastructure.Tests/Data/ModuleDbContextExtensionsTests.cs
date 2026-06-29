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

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }
}
