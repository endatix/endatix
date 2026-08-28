using Endatix.Core.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Base implementation of the Unit of Work pattern using Entity Framework Core.
/// This generic base class can work with any DbContext.
/// </summary>
/// <typeparam name="TContext">The type of DbContext to work with.</typeparam>
public abstract class EfUnitOfWorkBase<TContext> : IUnitOfWork
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly ILogger _logger;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfUnitOfWorkBase{TContext}"/> class.
    /// </summary>
    /// <param name="context">The Entity Framework Core DbContext.</param>
    /// <param name="logger">
    /// Records rollback failures, which are never surfaced to the caller. Optional so a test can
    /// construct a unit of work without wiring logging; DI supplies the real one.
    /// </param>
    protected EfUnitOfWorkBase(TContext context, ILogger? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        EnsureTransactionStarted();

        try
        {
            await _transaction!.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Rollback is cleanup, and cleanup runs on the failure path - so it neither throws nor observes
    /// <paramref name="cancellationToken"/>:
    /// <list type="bullet">
    /// <item>No active transaction is the desired end state, not an error. Throwing here turned a
    /// failed <c>BeginTransactionAsync</c> into "Transaction not started", masking the real cause.</item>
    /// <item>A rollback failure is not actionable - the transaction is dead either way - and letting it
    /// propagate out of a <c>catch</c> block replaces the exception the caller was actually handling.</item>
    /// <item>The token that cancelled the work must not also cancel undoing it, or a cancelled request
    /// leaves the transaction open until disposal.</item>
    /// </list>
    /// </remarks>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        var transaction = _transaction;
        _transaction = null;

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException, "Failed to roll back the transaction");
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            catch (Exception disposeException)
            {
                _logger.LogError(disposeException, "Failed to dispose the rolled back transaction");
            }
        }
    }

    /// <summary>
    /// Ensures that a transaction has been started.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no transaction has been started.</exception>
    private void EnsureTransactionStarted()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("Transaction not started. Call BeginTransactionAsync first.");
        }
    }
}
