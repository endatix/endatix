using Endatix.Core.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfUnitOfWorkBase{TContext}"/> class.
    /// </summary>
    /// <param name="context">The Entity Framework Core DbContext.</param>
    protected EfUnitOfWorkBase(TContext context)
    {
        _context = context;
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
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        EnsureTransactionStarted();

        try
        {
            await _transaction!.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
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