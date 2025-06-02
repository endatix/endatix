namespace Endatix.Core.Abstractions.Data
{
    /// <summary>
    /// This interface represents the Unit of Work pattern in the application.
    /// It maintains a list of objects affected by a business transaction and coordinates the writing out of changes and the resolution of concurrency problems.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Asynchronously saves all changes made in this context to the database.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously begins a new transaction.
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously commits the transaction.
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously rolls back the transaction.
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
} 