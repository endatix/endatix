using Endatix.Infrastructure.Data.Locking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.SqlServer.Locking;

/// <summary>
/// No transaction lock on SQL Server yet, so the work that needs one fails when it asks for it, with a reason,
/// rather than running unprotected; everything else on a SQL Server host is unaffected.
/// </summary>
/// <remarks>
/// The lock this stands in for is <c>sp_getapplock</c> with <c>@LockOwner = 'Transaction'</c>, the request as
/// <c>@Resource</c>, its mode as <c>@LockMode</c> and its timeout as <c>@LockTimeout</c>.
/// </remarks>
public sealed class SqlServerTransactionLock : ITransactionLock
{
    /// <inheritdoc />
    public Task AcquireAsync(
        DatabaseFacade database,
        TransactionLockRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException(
            $"Transaction lock {request} is not available on SQL Server yet; the work that needs it runs on PostgreSQL only."));
}
