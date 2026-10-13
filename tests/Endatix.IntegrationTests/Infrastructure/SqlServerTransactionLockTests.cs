using System.Data;
using System.Data.Common;
using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Persistence.SqlServer.Locking;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests;

/// <summary>
/// The SQL Server transaction lock against a real server: waits, timeouts and modes, each with two transactions on
/// their own connections.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed class SqlServerTransactionLockTests(DbIntegrationFixture fixture)
{
    private const int CommandTimeoutSeconds = 1;
    private const int CommandTimeoutNumber = -2;
    private const byte MaxInformationalClass = 10;
    private const byte MinBrokenConnectionClass = 20;

    // PRINT rather than RAISERROR WITH NOWAIT: the server holds a PRINT back until the batch ends, so it reaches
    // SqlClient together with the cancel. A message flushed during the wait makes SqlClient ignore the token until
    // the command times out.
    private const string InformationalMessageSql = "PRINT N'Waiting for the lock.';";

    private static readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan LongWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DeadlockDetection = TimeSpan.FromSeconds(25);
    private static readonly TransactionLockRequest Exclusive = new(TransactionLockScopes.ReportingFormSchema, "lock-test");
    private static readonly TransactionLockRequest Shared = Exclusive with { Mode = TransactionLockMode.Shared };

    private readonly SqlServerTransactionLock _lock = new();

    [Fact]
    public async Task AcquireAsync_WhileAnotherTransactionHoldsTheLock_ThrowsTransactionLockTimeoutException()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync();

        // Act
        var act = () => _lock.AcquireAsync(waiter.Database, Exclusive with { Timeout = ShortWait }, Cancellation);

        // Assert
        var thrown = await act.Should().ThrowAsync<TransactionLockTimeoutException>();
        thrown.Which.Request.Should().Be(Exclusive with { Timeout = ShortWait });
    }

    [Fact]
    public async Task AcquireAsync_AfterTheHolderCommits_TakesTheLock()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync();
        var waiting = _lock.AcquireAsync(waiter.Database, Exclusive with { Timeout = LongWait }, Cancellation);
        await Task.Delay(ShortWait, Cancellation);
        var waitedForTheHolder = !waiting.IsCompleted;

        // Act
        await holder.Database.CommitTransactionAsync(Cancellation);

        // Assert
        waitedForTheHolder.Should().BeTrue();
        await waiting.WaitAsync(LongWait, Cancellation);
        waiting.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_AfterTheHolderRollsBack_TakesTheLock()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync();
        var waiting = _lock.AcquireAsync(waiter.Database, Exclusive with { Timeout = LongWait }, Cancellation);
        await Task.Delay(ShortWait, Cancellation);
        var waitedForTheHolder = !waiting.IsCompleted;

        // Act
        await holder.Database.RollbackTransactionAsync(Cancellation);

        // Assert
        waitedForTheHolder.Should().BeTrue();
        await waiting.WaitAsync(LongWait, Cancellation);
        waiting.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_AfterTheHolderIsDisposedWithoutCommit_TakesTheLock()
    {
        // Arrange
        var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync();

        // Act
        await holder.DisposeAsync();

        // Assert
        var act = () => _lock.AcquireAsync(waiter.Database, Exclusive with { Timeout = LongWait }, Cancellation);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcquireAsync_TwoTransactionsWaitingForEachOther_FailsOneAsDeadlockVictimAndGrantsTheOther()
    {
        // Arrange
        var lockA = Exclusive with { Key = "deadlock-a" };
        var lockB = Exclusive with { Key = "deadlock-b" };
        await using var first = await BeginAsync();
        await _lock.AcquireAsync(first.Database, lockA, Cancellation);
        await using var second = await BeginAsync();
        await _lock.AcquireAsync(second.Database, lockB, Cancellation);
        var firstWaiting = _lock.AcquireAsync(first.Database, lockB, Cancellation);
        await Task.Delay(ShortWait, Cancellation);

        // Act
        var secondWaiting = _lock.AcquireAsync(second.Database, lockA, Cancellation);
        var victim = await Task.WhenAny(firstWaiting, secondWaiting).WaitAsync(DeadlockDetection, Cancellation);

        // Assert
        await victim.Awaiting(wait => wait).Should().ThrowAsync<InvalidOperationException>().WithMessage("*deadlock victim*");
        var (victimContext, survivor) = victim == firstWaiting ? (first, secondWaiting) : (second, firstWaiting);
        await victimContext.Database.RollbackTransactionAsync(Cancellation);
        await survivor.WaitAsync(LongWait, Cancellation);
        survivor.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_WithTimeout_LeavesTheRestOfTheTransactionWithoutIt()
    {
        // Arrange
        await using var context = await BeginAsync();
        var before = await LockTimeoutAsync(context);

        // Act
        await _lock.AcquireAsync(context.Database, Exclusive with { Timeout = ShortWait }, Cancellation);

        // Assert
        (await LockTimeoutAsync(context)).Should().Be(before);
    }

    [Fact]
    public async Task AcquireAsync_SharedWhileAnotherHoldsItShared_DoesNotWait()
    {
        // Arrange
        await using var first = await BeginAsync();
        await _lock.AcquireAsync(first.Database, Shared, Cancellation);
        await using var second = await BeginAsync();

        // Act
        var act = () => _lock.AcquireAsync(second.Database, Shared with { Timeout = ShortWait }, Cancellation);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcquireAsync_ExclusiveWhileAnotherHoldsItShared_Waits()
    {
        // Arrange
        await using var reader = await BeginAsync();
        await _lock.AcquireAsync(reader.Database, Shared, Cancellation);
        await using var writer = await BeginAsync();

        // Act
        var act = () => _lock.AcquireAsync(writer.Database, Exclusive with { Timeout = ShortWait }, Cancellation);

        // Assert
        await act.Should().ThrowAsync<TransactionLockTimeoutException>();
    }

    [Fact]
    public async Task AcquireAsync_SharedWhileAnotherHoldsItExclusive_Waits()
    {
        // Arrange
        await using var writer = await BeginAsync();
        await _lock.AcquireAsync(writer.Database, Exclusive, Cancellation);
        await using var reader = await BeginAsync();

        // Act
        var act = () => _lock.AcquireAsync(reader.Database, Shared with { Timeout = ShortWait }, Cancellation);

        // Assert
        await act.Should().ThrowAsync<TransactionLockTimeoutException>();
    }

    [Fact]
    public async Task AcquireAsync_OtherKeyWhileAnotherHoldsTheLock_DoesNotWait()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var other = await BeginAsync();

        // Act
        var act = () => _lock.AcquireAsync(
            other.Database, Exclusive with { Key = "other-lock-test", Timeout = ShortWait }, Cancellation);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcquireAsync_SameKeyInOtherScopeWhileAnotherHoldsTheLock_DoesNotWait()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var other = await BeginAsync();

        // Act
        var act = () => _lock.AcquireAsync(
            other.Database,
            Exclusive with { Scope = TransactionLockScopes.AudienceMatchKey, Timeout = ShortWait },
            Cancellation);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcquireAsync_CancelledWhileWaiting_ThrowsOperationCanceledException()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(ShortWait);

        // Act
        var act = () => _lock.AcquireAsync(waiter.Database, Exclusive, cancellation.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.InnerException.Should().BeOfType<SqlException>();
    }

    [Fact]
    public async Task AcquireAsync_CommandTimesOutWhileTheTokenIsCancelled_ThrowsTheSqlExceptionAsItIs()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        await using var waiter = await BeginAsync(options => options
            .UseSqlServer(fixture.ConnectionString, sql => sql.CommandTimeout(CommandTimeoutSeconds))
            .AddInterceptors(new CancelOnCommandFailure(cancellation)));

        // Act
        var act = () => _lock.AcquireAsync(waiter.Database, Exclusive, cancellation.Token);

        // Assert
        var thrown = await act.Should().ThrowExactlyAsync<SqlException>();
        thrown.Which.Number.Should().Be(CommandTimeoutNumber);
        cancellation.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_CancelledWhileWaitingAfterAnInformationalMessage_ThrowsOperationCanceledException()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync(options => options
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(new PrependToCommand(InformationalMessageSql)));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        cancellation.CancelAfter(ShortWait);

        // Act
        var act = () => _lock.AcquireAsync(waiter.Database, Exclusive, cancellation.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Errors.Cast<SqlError>().Should().Contain(error => error.Class <= MaxInformationalClass);
    }

    [Fact]
    public async Task AcquireAsync_WaitingSessionKilledWhileTheTokenIsCancelled_ThrowsTheSqlExceptionAsItIs()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        await using var waiter = await BeginAsync(options => options
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(new CancelOnCommandFailure(cancellation)));
        var waiterSession = await SessionIdAsync(waiter);
        var waiting = _lock.AcquireAsync(waiter.Database, Exclusive, cancellation.Token);
        await Task.Delay(ShortWait, Cancellation);
        var waitedForTheHolder = !waiting.IsCompleted;

        // Act
        await KillAsync(waiterSession);

        // Assert
        waitedForTheHolder.Should().BeTrue();
        var thrown = await waiting.Awaiting(wait => wait).Should().ThrowExactlyAsync<SqlException>();
        thrown.Which.Class.Should().BeGreaterThanOrEqualTo(MinBrokenConnectionClass);
        cancellation.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_ManyTransactionsOnOneKey_HoldItOneAtATime()
    {
        // Arrange
        const int transactions = 20;
        var holders = 0;
        var overlapped = false;

        async Task HoldBrieflyAsync()
        {
            await using var context = await BeginAsync();
            await _lock.AcquireAsync(context.Database, Exclusive with { Timeout = LongWait }, Cancellation);
            if (Interlocked.Increment(ref holders) > 1)
            {
                overlapped = true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), Cancellation);
            Interlocked.Decrement(ref holders);
            await context.Database.CommitTransactionAsync(Cancellation);
        }

        // Act
        await Task.WhenAll(Enumerable.Range(0, transactions).Select(_ => Task.Run(HoldBrieflyAsync, Cancellation)));

        // Assert
        overlapped.Should().BeFalse();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Task<DbContext> BeginAsync() =>
        BeginAsync(options => options.UseSqlServer(fixture.ConnectionString));

    private static async Task<DbContext> BeginAsync(Action<DbContextOptionsBuilder> configure)
    {
        DbContextOptionsBuilder options = new();
        configure(options);
        DbContext context = new(options.Options);
        await context.Database.BeginTransactionAsync(Cancellation);
        return context;
    }

    private static Task<int> LockTimeoutAsync(DbContext context) =>
        context.Database
            .SqlQuery<int>($"SELECT @@LOCK_TIMEOUT AS [Value]")
            .SingleAsync(Cancellation);

    private static Task<short> SessionIdAsync(DbContext context) =>
        context.Database
            .SqlQuery<short>($"SELECT @@SPID AS [Value]")
            .SingleAsync(Cancellation);

    // KILL takes no variable and cannot run inside a transaction, so it is built on the server on a connection of its own.
    private async Task KillAsync(short session)
    {
        await using SqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using SqlCommand kill = new(
            "DECLARE @kill nvarchar(20) = N'KILL ' + CAST(@session AS nvarchar(10)); EXEC (@kill);", connection);
        kill.Parameters.Add(new SqlParameter("@session", SqlDbType.SmallInt) { Value = session });
        await kill.ExecuteNonQueryAsync(Cancellation);
    }

    /// <summary>Puts <paramref name="sql"/> in front of every command, in the same batch.</summary>
    private sealed class PrependToCommand(string sql) : DbCommandInterceptor
    {
        public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
        {
            result.CommandText = sql + Environment.NewLine + result.CommandText;
            return result;
        }
    }

    /// <summary>
    /// Cancels the token once the command has failed and before its exception reaches the lock, so the lock sees a
    /// failure that is not SqlClient's cancellation while the token is cancelled, every time.
    /// </summary>
    private sealed class CancelOnCommandFailure(CancellationTokenSource cancellation) : DbCommandInterceptor
    {
        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default) =>
            cancellation.CancelAsync();
    }
}
