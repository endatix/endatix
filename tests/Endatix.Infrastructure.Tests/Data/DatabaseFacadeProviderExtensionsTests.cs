using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data;

public class DatabaseFacadeProviderExtensionsTests
{
    [Fact]
    public void RequirePostgreSql_NpgsqlDatabase_DoesNotThrow()
    {
        // Arrange
        using var context = NpgsqlContext();

        // Act
        var require = () => context.Database.RequirePostgreSql(typeof(DbContext));

        // Assert
        require.Should().NotThrow();
    }

    [Fact]
    public void RequirePostgreSql_SqlServerDatabase_ThrowsNamingTheContextAndTheProvider()
    {
        // Arrange
        using var context = SqlServerContext();

        // Act
        var require = () => context.Database.RequirePostgreSql(typeof(DatabaseFacadeProviderExtensionsTests));

        // Assert
        require.Should().Throw<NotSupportedException>().WithMessage(
            "DatabaseFacadeProviderExtensionsTests is mapped for PostgreSQL only, but it is configured for the " +
            "'Microsoft.EntityFrameworkCore.SqlServer' provider.*");
    }

    [Fact]
    public void RequireSqlServer_SqlServerDatabase_DoesNotThrow()
    {
        // Arrange
        using var context = SqlServerContext();

        // Act
        var require = () => context.Database.RequireSqlServer(typeof(DbContext));

        // Assert
        require.Should().NotThrow();
    }

    [Fact]
    public void RequireSqlServer_NpgsqlDatabase_ThrowsNamingTheContextAndTheProvider()
    {
        // Arrange
        using var context = NpgsqlContext();

        // Act
        var require = () => context.Database.RequireSqlServer(typeof(DatabaseFacadeProviderExtensionsTests));

        // Assert
        require.Should().Throw<NotSupportedException>().WithMessage(
            "DatabaseFacadeProviderExtensionsTests is mapped for SQL Server only, but it is configured for the " +
            "'Npgsql.EntityFrameworkCore.PostgreSQL' provider.*");
    }

    private static DbContext NpgsqlContext() =>
        new(new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=provider_guard_tests").Options);

    private static DbContext SqlServerContext() =>
        new(new DbContextOptionsBuilder().UseSqlServer("Server=localhost;Database=ProviderGuardTests").Options);
}
