namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// A transaction lock was still held by another transaction when the wait for it ran out. The work did not run
/// and its transaction must be rolled back (PostgreSQL has already aborted it); trying again later is safe.
/// </summary>
public sealed class TransactionLockTimeoutException : TimeoutException
{
    /// <param name="request">The lock that was not acquired.</param>
    /// <param name="innerException">
    /// The database error that reported the timeout, or <see langword="null"/> when the database reported it as a
    /// result rather than an error.
    /// </param>
    public TransactionLockTimeoutException(TransactionLockRequest request, Exception? innerException = null)
        : base($"Timed out after {request.Timeout} waiting for transaction lock {request}.", innerException)
    {
        Request = request;
    }

    /// <summary>The lock that was not acquired.</summary>
    public TransactionLockRequest Request { get; }
}
