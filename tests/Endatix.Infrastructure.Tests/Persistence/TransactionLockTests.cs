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
    private const byte InformationalClass = 0;
    private const byte MaxInformationalClass = 10;
    private const byte ClientCancelClass = 11;
    private const byte BrokenConnectionClass = 20;
    private const int CommandTimeoutNumber = -2;
    private const int DeadlockVictimNumber = 1205;
    private const byte DeadlockVictimClass = 13;
    private const int KilledSessionNumber = 596;
    private const byte KilledSessionClass = 21;

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
    public void AddDbSpecificRepositories_OnSqlServer_RegistersTheAppLock()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        new SqlServerPersistenceBuilder(services).AddDbSpecificRepositories().AddDbSpecificRepositories();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetServices<ITransactionLock>().Should().ContainSingle()
            .Which.Should().BeOfType<SqlServerTransactionLock>();
    }

    [Fact]
    public async Task AcquireAsync_OnSqlServerWithoutTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var context = CreateContext(options => options.UseSqlServer("Server=unused;Database=unused"));
        SqlServerTransactionLock transactionLock = new();

        // Act
        var act = () => transactionLock.AcquireAsync(context.Database, Request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*1:tenant:form*inside a transaction*");
    }

    /// <summary>
    /// SQL Server hashes a resource name past its first 32 characters, so a form's lock, which must never be shared,
    /// stays within them for every id.
    /// </summary>
    [Fact]
    public void ResourceName_FormLockOfTheLargestId_StaysWithinTheCharactersKeptAsTheyAre()
    {
        // Arrange
        var request = FormSubmissionsLock.Deleting(long.MaxValue);

        // Act
        var name = SqlServerTransactionLock.ResourceName(request);

        // Assert
        name.Length.Should().BeLessThanOrEqualTo(32);
    }

    [Fact]
    public void ResourceName_ShortKey_IsScopeAndKey()
    {
        // Arrange
        TransactionLockRequest request = new(Scope: 1117, Key: "12:34");

        // Act
        var name = SqlServerTransactionLock.ResourceName(request);

        // Assert
        name.Should().Be("1117:12:34");
    }

    [Fact]
    public void ResourceName_KeyFillingTheLimit_IsUsedAsItIs()
    {
        // Arrange
        TransactionLockRequest request = new(Scope: 1, Key: new string('k', SqlServerTransactionLock.MaxResourceLength - 2));

        // Act
        var name = SqlServerTransactionLock.ResourceName(request);

        // Assert
        name.Should().Be(request.ToString()).And.HaveLength(SqlServerTransactionLock.MaxResourceLength);
    }

    [Fact]
    public void ResourceName_KeyOverTheLimit_IsTheScopeAndTheKeyHash()
    {
        // Arrange
        TransactionLockRequest request = new(Scope: 1, Key: new string('k', SqlServerTransactionLock.MaxResourceLength));

        // Act
        var name = SqlServerTransactionLock.ResourceName(request);

        // Assert
        name.Should().MatchRegex("^1#[0-9A-F]{64}$");
    }

    [Fact]
    public void ResourceName_SameLongKey_IsTheSameName()
    {
        // Arrange
        var key = new string('k', 300);

        // Act
        var first = SqlServerTransactionLock.ResourceName(new TransactionLockRequest(Scope: 1, Key: key));
        var second = SqlServerTransactionLock.ResourceName(new TransactionLockRequest(Scope: 1, Key: key));

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public void ResourceName_LongKeysDifferingOnlyAfterTheLimit_AreDifferentNames()
    {
        // Arrange
        var prefix = new string('k', 300);

        // Act
        var first = SqlServerTransactionLock.ResourceName(new TransactionLockRequest(Scope: 1, Key: prefix + "a"));
        var second = SqlServerTransactionLock.ResourceName(new TransactionLockRequest(Scope: 1, Key: prefix + "b"));

        // Assert
        first.Should().NotBe(second);
    }

    [Fact]
    public void ResourceName_SameLongKeyInOtherScopes_AreDifferentNames()
    {
        // Arrange
        var key = new string('k', 300);

        // Act
        var first = SqlServerTransactionLock.ResourceName(new TransactionLockRequest(Scope: 1, Key: key));
        var second = SqlServerTransactionLock.ResourceName(new TransactionLockRequest(Scope: 2, Key: key));

        // Assert
        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Failure_GrantedStatus_IsNone(int status)
    {
        // Arrange
        var granted = status;

        // Act
        var failure = SqlServerTransactionLock.Failure(granted, Request);

        // Assert
        failure.Should().BeNull();
    }

    [Fact]
    public void Failure_TimedOutStatus_IsTransactionLockTimeoutException()
    {
        // Arrange
        const int timedOut = -1;

        // Act
        var failure = SqlServerTransactionLock.Failure(timedOut, Request);

        // Assert
        failure.Should().BeOfType<TransactionLockTimeoutException>().Which.Request.Should().Be(Request);
    }

    [Fact]
    public void Failure_CancelledStatus_IsInvalidOperationExceptionSayingTheServerCancelledIt()
    {
        // Arrange
        const int cancelled = -2;

        // Act
        var failure = SqlServerTransactionLock.Failure(cancelled, Request);

        // Assert
        failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Match("*1:tenant:form*cancelled by SQL Server*");
    }

    [Fact]
    public void Failure_DeadlockStatus_IsInvalidOperationExceptionSayingItWasTheVictim()
    {
        // Arrange
        const int deadlockVictim = -3;

        // Act
        var failure = SqlServerTransactionLock.Failure(deadlockVictim, Request);

        // Assert
        failure.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Match("*deadlock victim*");
    }

    [Fact]
    public void Failure_InvalidCallStatus_IsInvalidOperationExceptionWithTheStatus()
    {
        // Arrange
        const int invalidCall = -999;

        // Act
        var failure = SqlServerTransactionLock.Failure(invalidCall, Request);

        // Assert
        failure.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Match("*refused*status -999*");
    }

    public static TheoryData<(int Number, byte Class)[]> ClientCancellations =>
    new()
    {
        new[] { (0, ClientCancelClass) },
        new[] { (0, ClientCancelClass), (0, ClientCancelClass) },
        new[] { (0, InformationalClass), (0, ClientCancelClass) },
        new[] { (50000, MaxInformationalClass), (0, ClientCancelClass), (0, ClientCancelClass) },
    };

    public static TheoryData<(int Number, byte Class)[]> OtherFailures =>
    new()
    {
        Array.Empty<(int, byte)>(),
        new[] { (0, InformationalClass) },
        new[] { (CommandTimeoutNumber, ClientCancelClass) },
        new[] { (CommandTimeoutNumber, ClientCancelClass), (0, ClientCancelClass), (0, InformationalClass) },
        new[] { (DeadlockVictimNumber, DeadlockVictimClass), (0, ClientCancelClass) },
        new[] { (0, ClientCancelClass), (0, BrokenConnectionClass) },
        new[] { (KilledSessionNumber, KilledSessionClass), (0, BrokenConnectionClass) },
    };

    [Theory]
    [MemberData(nameof(ClientCancellations))]
    public void IsCancelledByClient_SqlClientCancellationWithOnlyInformationalMessagesBeside_IsTrue(
        (int Number, byte Class)[] thrownErrors)
    {
        // Act
        var cancelled = SqlServerTransactionLock.IsCancelledByClient(thrownErrors);

        // Assert
        cancelled.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(OtherFailures))]
    public void IsCancelledByClient_NoSqlClientCancellationOrAnErrorBesideIt_IsFalse(
        (int Number, byte Class)[] thrownErrors)
    {
        // Act
        var cancelled = SqlServerTransactionLock.IsCancelledByClient(thrownErrors);

        // Assert
        cancelled.Should().BeFalse();
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
