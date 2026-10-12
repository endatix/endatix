using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Tests.Data;

public class DatabaseFacadeProviderExtensionsTests
{
    [Fact]
    public void EnsureNpgsql_NpgsqlDatabase_DoesNotThrow()
    {
        // Arrange
        using var context = NpgsqlContext();

        // Act
        var ensure = () => context.Database.EnsureNpgsql(typeof(DbContext));

        // Assert
        ensure.Should().NotThrow();
    }

    [Fact]
    public void EnsureNpgsql_SqlServerDatabase_ThrowsNamingTheContextAndTheProvider()
    {
        // Arrange
        using var context = SqlServerContext();

        // Act
        var ensure = () => context.Database.EnsureNpgsql(typeof(DatabaseFacadeProviderExtensionsTests));

        // Assert
        ensure.Should().Throw<NotSupportedException>().WithMessage(
            "DatabaseFacadeProviderExtensionsTests is mapped for PostgreSQL only, but it is configured for the " +
            "'Microsoft.EntityFrameworkCore.SqlServer' provider.*");
    }

    [Fact]
    public void EnsureSqlServer_SqlServerDatabase_DoesNotThrow()
    {
        // Arrange
        using var context = SqlServerContext();

        // Act
        var ensure = () => context.Database.EnsureSqlServer(typeof(DbContext));

        // Assert
        ensure.Should().NotThrow();
    }

    [Fact]
    public void EnsureSqlServer_NpgsqlDatabase_ThrowsNamingTheContextAndTheProvider()
    {
        // Arrange
        using var context = NpgsqlContext();

        // Act
        var ensure = () => context.Database.EnsureSqlServer(typeof(DatabaseFacadeProviderExtensionsTests));

        // Assert
        ensure.Should().Throw<NotSupportedException>().WithMessage(
            "DatabaseFacadeProviderExtensionsTests is mapped for SQL Server only, but it is configured for the " +
            "'Npgsql.EntityFrameworkCore.PostgreSQL' provider.*");
    }

    private static DbContext NpgsqlContext() =>
        new(new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=provider_guard_tests").Options);

    private static DbContext SqlServerContext() =>
        new(new DbContextOptionsBuilder().UseSqlServer("Server=localhost;Database=ProviderGuardTests").Options);
}
