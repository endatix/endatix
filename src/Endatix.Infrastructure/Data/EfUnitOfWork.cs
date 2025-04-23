using Endatix.Core.Abstractions.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Data
{
    /// <summary>
    /// Implements the Unit of Work pattern using Entity Framework Core.
    /// </summary>
    public class EfUnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        /// <summary>
        /// Initializes a new instance of the <see cref="EfUnitOfWork"/> class.
        /// </summary>
        /// <param name="context">The Entity Framework Core DbContext.</param>
        public EfUnitOfWork(AppDbContext context)
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
                await _transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }

        /// <inheritdoc/>
        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            EnsureTransactionStarted();

            try
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }

        /// <summary>
        /// Ensures that the transaction is started.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the transaction is not started.</exception>
        private void EnsureTransactionStarted()
        {
            if (_transaction is null)
            {
                throw new InvalidOperationException($"Transaction is not started. Please call {nameof(BeginTransactionAsync)} method first.");
            }
        }
    }
}