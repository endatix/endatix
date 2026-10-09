namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// A transaction lock was still held by another transaction when the wait for it ran out. The work did not run
/// and its transaction is aborted; trying again later is safe.
/// </summary>
public sealed class TransactionLockTimeoutException : TimeoutException
{
    public TransactionLockTimeoutException(TransactionLockRequest request, Exception innerException)
        : base($"Timed out after {request.Timeout} waiting for transaction lock {request}.", innerException)
    {
        Request = request;
    }

    /// <summary>The lock that was not acquired.</summary>
    public TransactionLockRequest Request { get; }
}
