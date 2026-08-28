using Endatix.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Infrastructure.Tests.Data;

public class AppUnitOfWorkTests
{
    private readonly AppDbContext _appDbContext;
    private readonly IDbContextTransaction _dbTransaction;
    private readonly AppUnitOfWork _sut;

    public AppUnitOfWorkTests()
    {
        _appDbContext = Substitute.For<AppDbContext>();
        _dbTransaction = Substitute.For<IDbContextTransaction>();

        var database = Substitute.For<DatabaseFacade>(_appDbContext);
        _appDbContext.Database.Returns(database);

        _sut = new AppUnitOfWork(_appDbContext);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldCallContextSaveChanges()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.SaveChangesAsync(cancellationToken).Returns(1);

        // Act
        var result = await _sut.SaveChangesAsync(cancellationToken);

        // Assert
        result.Should().Be(1);
        await _appDbContext.Received(1).SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldStartNewTransaction()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);

        // Act
        await _sut.BeginTransactionAsync(cancellationToken);

        // Assert
        await _appDbContext.Database.Received(1).BeginTransactionAsync(cancellationToken);
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenTransactionNotStarted_ShouldThrowInvalidOperationException()
    {
        // Act
        var act = () => _sut.CommitTransactionAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction not started. Call BeginTransactionAsync first.");
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenTransactionStarted_ShouldCommitAndDisposeTransaction()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        // Act
        await _sut.CommitTransactionAsync(cancellationToken);

        // Assert
        await _dbTransaction.Received(1).CommitAsync(cancellationToken);
        await _dbTransaction.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenCommitFails_ShouldStillDisposeTransaction()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        _dbTransaction.CommitAsync(cancellationToken)
            .Throws(new DbUpdateException("Commit failed"));

        // Act
        var act = () => _sut.CommitTransactionAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await _dbTransaction.Received(1).DisposeAsync();
    }

    /// <summary>
    /// No active transaction is the state rollback exists to reach, so asking for it again is a no-op.
    /// Throwing here meant a failed <c>BeginTransactionAsync</c> came back as "Transaction not started"
    /// from the caller's catch block, hiding the connection failure that actually caused it.
    /// </summary>
    [Fact]
    public async Task RollbackTransactionAsync_WhenTransactionNotStarted_ShouldDoNothing()
    {
        // Act
        var act = () => _sut.RollbackTransactionAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenTransactionStarted_ShouldRollbackAndDisposeTransaction()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        // Act
        await _sut.RollbackTransactionAsync(cancellationToken);

        // Assert
        await _dbTransaction.Received(1).RollbackAsync(cancellationToken);
        await _dbTransaction.Received(1).DisposeAsync();
    }

    /// <summary>
    /// Callers roll back from a catch block. A rollback failure escaping there would replace the
    /// exception being handled - the only one worth diagnosing - so it is logged and swallowed.
    /// </summary>
    [Fact]
    public async Task RollbackTransactionAsync_WhenRollbackFails_ShouldSwallowAndStillDisposeTransaction()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        _dbTransaction.RollbackAsync(Arg.Any<CancellationToken>())
            .Throws(new DbUpdateException("Rollback failed"));

        // Act
        var act = () => _sut.RollbackTransactionAsync(cancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await _dbTransaction.Received(1).DisposeAsync();
    }

    /// <summary>
    /// Disposal is the second way rollback can fail, and it must not escape either.
    /// </summary>
    [Fact]
    public async Task RollbackTransactionAsync_WhenDisposeFails_ShouldSwallow()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        _dbTransaction.DisposeAsync()
            .Throws(new DbUpdateException("Dispose failed"));

        // Act
        var act = () => _sut.RollbackTransactionAsync(cancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The token that cancelled the work must not also cancel undoing it, or a cancelled request
    /// leaves the transaction open until the context is disposed.
    /// </summary>
    [Fact]
    public async Task RollbackTransactionAsync_WhenTokenAlreadyCancelled_ShouldStillRollBack()
    {
        // Arrange
        _appDbContext.Database.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(CancellationToken.None);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        await _sut.RollbackTransactionAsync(cts.Token);

        // Assert
        await _dbTransaction.Received(1).RollbackAsync(CancellationToken.None);
        await _dbTransaction.Received(1).DisposeAsync();
    }

    /// <summary>
    /// A rolled back transaction is finished, so a second rollback must not touch it again.
    /// </summary>
    [Fact]
    public async Task RollbackTransactionAsync_CalledTwice_ShouldRollBackOnce()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        // Act
        await _sut.RollbackTransactionAsync(cancellationToken);
        await _sut.RollbackTransactionAsync(cancellationToken);

        // Assert
        await _dbTransaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _dbTransaction.Received(1).DisposeAsync();
    }
}