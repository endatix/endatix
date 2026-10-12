using Endatix.Infrastructure.Data.Locking;
using Endatix.Persistence.PostgreSql.Builders;
using Endatix.Persistence.PostgreSql.Locking;
using Endatix.Persistence.SqlServer.Builders;
using Endatix.Persistence.SqlServer.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Persistence;

public class TransactionLockTests
{
    private static readonly TransactionLockRequest Request = new(Scope: 1, Key: "tenant:form");

    [Fact]
    public void AddDbSpecificRepositories_OnPostgreSql_RegistersTheAdvisoryLock()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        new PostgreSqlPersistenceBuilder(services).AddDbSpecificRepositories().AddDbSpecificRepositories();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetServices<ITransactionLock>().Should().ContainSingle()
            .Which.Should().BeOfType<PostgreSqlTransactionLock>();
    }

    [Fact]
    public async Task AcquireAsync_OnSqlServer_ThrowsNotSupportedException()
    {
        // Arrange
        ServiceCollection services = new();
        new SqlServerPersistenceBuilder(services).AddDbSpecificRepositories();
        using var provider = services.BuildServiceProvider();
        await using var context = CreateContext(options => options.UseSqlServer("Server=unused;Database=unused"));

        // Act
        var act = () => provider.GetRequiredService<ITransactionLock>()
            .AcquireAsync(context.Database, Request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*1:tenant:form*SQL Server*");
    }

    [Fact]
    public async Task AcquireAsync_OnPostgreSqlWithoutTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var context = CreateContext(options => options.UseNpgsql("Host=unused"));
        PostgreSqlTransactionLock transactionLock = new();

        // Act
        var act = () => transactionLock.AcquireAsync(context.Database, Request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inside a transaction*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Timeout_NotPositive_ThrowsArgumentOutOfRangeException(int milliseconds)
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(milliseconds);

        // Act
        var act = () => Request with { Timeout = timeout };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static DbContext CreateContext(Action<DbContextOptionsBuilder> configure)
    {
        DbContextOptionsBuilder builder = new();
        configure(builder);
        return new DbContext(builder.Options);
    }
}
