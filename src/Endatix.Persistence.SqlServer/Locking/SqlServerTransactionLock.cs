using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.SqlServer.Locking;

/// <summary>
/// A transaction-owned application lock, <c>sp_getapplock</c> with <c>@LockOwner = 'Transaction'</c>, the request
/// as <c>@Resource</c>, its mode as <c>@LockMode</c> and its timeout as <c>@LockTimeout</c>.
/// </summary>
/// <remarks>
/// <para>
/// The resource is <c>{scope}:{key}</c>, which keeps every scope apart from every other. SQL Server compares it
/// byte for byte, whatever the collation, so keys are case-sensitive as on PostgreSQL. It holds at most 255
/// characters and SQL Server would cut a longer one short, so a longer key is replaced by its SHA-256 as
/// <c>{scope}#{hex}</c>; the <c>#</c> keeps that apart from every key short enough to be used as it is. SQL Server
/// hashes the resource into its lock table, so two resources can share a lock; they then only wait for each other.
/// </para>
/// <para>
/// A timeout is <c>@LockTimeout</c> in milliseconds and holds for this wait alone, like PostgreSQL's
/// <c>lock_timeout</c> set around the lock: the session's <c>LOCK_TIMEOUT</c>, which the rest of the transaction's
/// row locks wait under, is left as it was. Without one the wait is unbounded (<c>-1</c>) and ends with the
/// command timeout.
/// </para>
/// <para>
/// A lock that is not granted ends no transaction on SQL Server, unlike PostgreSQL, where the failed statement
/// aborts it. The transaction keeps the locks it already held, so the caller must roll it back, as it would on
/// PostgreSQL. Rolling it back here instead would leave the caller's transaction object completed under it.
/// </para>
/// </remarks>
public sealed class SqlServerTransactionLock : ITransactionLock
{
    /// <summary>The longest <c>@Resource</c> that <c>sp_getapplock</c> takes.</summary>
    internal const int MaxResourceLength = 255;

    private const string GetAppLockSql =
        "EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = @LockMode, @LockOwner = 'Transaction', @LockTimeout = @LockTimeout";

    private const int WaitWithoutTimeout = -1;

    /// <inheritdoc />
    public async Task AcquireAsync(
        DatabaseFacade database,
        TransactionLockRequest request,
        CancellationToken cancellationToken)
    {
        if (database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"Transaction lock {request} must be taken inside a transaction.");
        }

        var status = await GetAppLockAsync(database, request, cancellationToken);
        if (Failure(status, request, cancellationToken) is { } failure)
        {
            throw failure;
        }
    }

    /// <summary>
    /// The <c>@Resource</c> for <paramref name="request"/>: <c>{scope}:{key}</c>, or <c>{scope}#{SHA-256 of key}</c>
    /// when that is longer than <see cref="MaxResourceLength"/>.
    /// </summary>
    internal static string ResourceName(TransactionLockRequest request)
    {
        // Built with the invariant culture rather than by ToString, so every process names the lock alike.
        var name = string.Create(CultureInfo.InvariantCulture, $"{request.Scope}:{request.Key}");
        if (name.Length <= MaxResourceLength)
        {
            return name;
        }

        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Key)));
        return string.Create(CultureInfo.InvariantCulture, $"{request.Scope}#{keyHash}");
    }

    private static async Task<int> GetAppLockAsync(
        DatabaseFacade database,
        TransactionLockRequest request,
        CancellationToken cancellationToken)
    {
        SqlParameter status = new("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        try
        {
            await database.ExecuteSqlRawAsync(GetAppLockSql, [status, .. LockParameters(request)], cancellationToken);
        }
        catch (SqlException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw Cancelled(request, exception, cancellationToken);
        }

        return (int)status.Value;
    }

    private static SqlParameter[] LockParameters(TransactionLockRequest request) =>
    [
        new("@Resource", SqlDbType.NVarChar, MaxResourceLength) { Value = ResourceName(request) },
        new("@LockMode", SqlDbType.VarChar, 32) { Value = LockMode(request.Mode) },
        new("@LockTimeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds(request.Timeout) },
    ];

    private static string LockMode(TransactionLockMode mode) =>
        mode == TransactionLockMode.Shared ? "Shared" : "Exclusive";

    // sp_getapplock's return status: 0 granted at once, 1 granted after a wait, a negative value not granted.
    private static Exception? Failure(int status, TransactionLockRequest request, CancellationToken cancellationToken) =>
        status switch
        {
            >= 0 => null,
            -1 => new TransactionLockTimeoutException(request),
            -2 when cancellationToken.IsCancellationRequested => Cancelled(request, innerException: null, cancellationToken),
            -3 => new InvalidOperationException(
                $"The wait for transaction lock {request} was chosen as a deadlock victim; roll back the transaction and try again."),
            _ => new InvalidOperationException(
                $"sp_getapplock refused transaction lock {request} with status {status}."),
        };

    private static OperationCanceledException Cancelled(
        TransactionLockRequest request,
        Exception? innerException,
        CancellationToken cancellationToken) =>
        new($"The wait for transaction lock {request} was cancelled.", innerException, cancellationToken);

    // Rounded up to whole milliseconds, as a value under one would read as 0, which means not waiting at all.
    private static int LockTimeoutMilliseconds(TimeSpan? timeout) =>
        timeout is { } wait
            ? (int)Math.Min(Math.Ceiling(wait.TotalMilliseconds), int.MaxValue)
            : WaitWithoutTimeout;
}
