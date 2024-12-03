using Endatix.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Infrastructure.Tests.Data;

public class EfUnitOfWorkTests
{
    private readonly AppDbContext _appDbContext;
    private readonly IDbContextTransaction _dbTransaction;
    private readonly EfUnitOfWork _sut;

    public EfUnitOfWorkTests()
    {
        _appDbContext = Substitute.For<AppDbContext>();
        _dbTransaction = Substitute.For<IDbContextTransaction>();

        var database = Substitute.For<DatabaseFacade>(_appDbContext);
        _appDbContext.Database.Returns(database);

        _sut = new EfUnitOfWork(_appDbContext);
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
            .WithMessage("Transaction is not started. Please call BeginTransactionAsync method first.");
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

    [Fact]
    public async Task RollbackTransactionAsync_WhenTransactionNotStarted_ShouldThrowInvalidOperationException()
    {
        // Act
        var act = () => _sut.RollbackTransactionAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction is not started. Please call BeginTransactionAsync method first.");
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

    [Fact]
    public async Task RollbackTransactionAsync_WhenRollbackFails_ShouldStillDisposeTransaction()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        _appDbContext.Database.BeginTransactionAsync(cancellationToken).Returns(_dbTransaction);
        await _sut.BeginTransactionAsync(cancellationToken);

        _dbTransaction.RollbackAsync(cancellationToken)
            .Throws(new DbUpdateException("Rollback failed"));

        // Act
        var act = () => _sut.RollbackTransactionAsync(cancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        await _dbTransaction.Received(1).DisposeAsync();
    }
}