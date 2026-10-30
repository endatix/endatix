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
/// keeps the first 32 characters of a resource as they are and hashes the rest, so two resources that agree in their
/// first 32 characters can share a lock; they then only wait for each other. A scope whose waits are near zero keeps
/// its resources shorter than that (<see cref="TransactionLockScopes.FormSubmissions"/>).
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
/// <para>
/// Cancelling the token makes SqlClient abort the wait, which becomes an <see cref="OperationCanceledException"/>.
/// Any other <see cref="SqlException"/>, such as a command timeout or a killed session, is thrown as it is, even
/// when the token was cancelled as well.
/// </para>
/// </remarks>
public sealed class SqlServerTransactionLock : ITransactionLock
{
    /// <summary>The longest <c>@Resource</c> that <c>sp_getapplock</c> takes.</summary>
    internal const int MaxResourceLength = 255;

    private const string GetAppLockSql =
        "EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = @LockMode, @LockOwner = 'Transaction', @LockTimeout = @LockTimeout";

    private const int WaitWithoutTimeout = -1;

    private const byte CancelledByClientClass = 11;

    private const byte MaxInformationalClass = 10;

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
        if (Failure(status, request) is { } failure)
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
        catch (SqlException exception) when (cancellationToken.IsCancellationRequested && IsCancelledByClient(exception))
        {
            throw new OperationCanceledException(
                $"The wait for transaction lock {request} was cancelled.", exception, cancellationToken);
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

    private static bool IsCancelledByClient(SqlException exception) =>
        IsCancelledByClient(exception.Errors.Cast<SqlError>().Select(error => (error.Number, error.Class)).ToList());

    /// <summary>
    /// Whether <paramref name="errors"/>, as number and class, are SqlClient aborting the batch for a cancelled token:
    /// at least one error it raises itself, numbered 0 with class 11, and otherwise only informational messages
    /// (class 10 and below), which SqlClient hands over in the same exception. Its message is localized, so the number
    /// and class are what tell it apart. A command timeout (-2), a server error (a positive number) or a broken or
    /// killed connection (class 20 and up) is not, even when the token happens to be cancelled by the time it is thrown.
    /// </summary>
    internal static bool IsCancelledByClient(IReadOnlyCollection<(int Number, byte Class)> errors) =>
        errors.Any(IsCancelError)
        && errors.All(error => error.Class <= MaxInformationalClass || IsCancelError(error));

    private static bool IsCancelError((int Number, byte Class) error) =>
        error is (0, CancelledByClientClass);

    /// <summary>
    /// The exception for <c>sp_getapplock</c>'s return status, or <see langword="null"/> when the lock was granted:
    /// 0 at once, 1 after a wait. A negative status is not granted.
    /// </summary>
    /// <remarks>
    /// <c>-2</c>, cancelled, is not the caller's token: that aborts the batch before a status comes back. The server
    /// gave the request up itself, so it is a failure to report, not an <see cref="OperationCanceledException"/>,
    /// which callers take as their own cancellation and do not report.
    /// </remarks>
    internal static Exception? Failure(int status, TransactionLockRequest request) =>
        status switch
        {
            >= 0 => null,
            -1 => new TransactionLockTimeoutException(request),
            -2 => new InvalidOperationException(
                $"The wait for transaction lock {request} was cancelled by SQL Server; roll back the transaction and try again."),
            -3 => new InvalidOperationException(
                $"The wait for transaction lock {request} was chosen as a deadlock victim; roll back the transaction and try again."),
            _ => new InvalidOperationException(
                $"sp_getapplock refused transaction lock {request} with status {status}."),
        };

    // Rounded up to whole milliseconds, as a value under one would read as 0, which means not waiting at all.
    private static int LockTimeoutMilliseconds(TimeSpan? timeout) =>
        timeout is { } wait
            ? (int)Math.Min(Math.Ceiling(wait.TotalMilliseconds), int.MaxValue)
            : WaitWithoutTimeout;
}
