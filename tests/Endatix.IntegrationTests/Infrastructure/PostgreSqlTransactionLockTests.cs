using Endatix.Infrastructure.Data.Locking;
using Endatix.IntegrationTests.Shared;
using Endatix.Persistence.PostgreSql.Locking;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Endatix.IntegrationTests;

/// <summary>
/// The PostgreSQL transaction lock against a real server: waits, timeouts and modes, each with two transactions on
/// their own connections.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class PostgreSqlTransactionLockTests(DbIntegrationFixture fixture)
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(300);
    private static readonly TransactionLockRequest Exclusive = new(TransactionLockScopes.ReportingFormSchema, "lock-test");
    private static readonly TransactionLockRequest Shared = Exclusive with { Mode = TransactionLockMode.Shared };

    private readonly PostgreSqlTransactionLock _lock = new();

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
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.LockNotAvailable);
    }

    [Fact]
    public async Task AcquireAsync_AfterTheHolderCommits_TakesTheLock()
    {
        // Arrange
        await using var holder = await BeginAsync();
        await _lock.AcquireAsync(holder.Database, Exclusive, Cancellation);
        await using var waiter = await BeginAsync();
        var waiting = _lock.AcquireAsync(waiter.Database, Exclusive with { Timeout = TimeSpan.FromSeconds(10) }, Cancellation);

        // Act
        await holder.Database.CommitTransactionAsync(Cancellation);

        // Assert
        await waiting.WaitAsync(TimeSpan.FromSeconds(10), Cancellation);
        waiting.IsCompletedSuccessfully.Should().BeTrue();
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

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private async Task<DbContext> BeginAsync()
    {
        DbContext context = new(new DbContextOptionsBuilder().UseNpgsql(fixture.ConnectionString).Options);
        await context.Database.BeginTransactionAsync(Cancellation);
        return context;
    }

    private static Task<string> LockTimeoutAsync(DbContext context) =>
        context.Database
            .SqlQuery<string>($"SELECT current_setting('lock_timeout') AS \"Value\"")
            .SingleAsync(Cancellation);
}
