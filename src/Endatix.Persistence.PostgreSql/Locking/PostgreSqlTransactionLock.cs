using System.Globalization;
using System.Runtime.CompilerServices;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Endatix.Persistence.PostgreSql.Locking;

/// <summary>
/// A transaction-scoped advisory lock, <c>pg_advisory_xact_lock(scope, hashtext(key))</c>, or
/// <c>pg_advisory_xact_lock(id)</c> for a request with a 64-bit <see cref="TransactionLockRequest.Id"/>.
/// </summary>
/// <remarks>
/// <para>
/// The two-key form keeps every scope apart from every other. <c>hashtext</c> is 32 bits, so two keys of one scope
/// can share a lock; they then only wait for each other, which is harmless where waits are long. A scope whose waits
/// are near zero cannot afford that, so it keys by its 64-bit id in the one-key form, which the two-key form never
/// shares (<see cref="TransactionLockScopes.FormSubmissions"/>).
/// </para>
/// <para>
/// A timeout is a <c>lock_timeout</c> for this wait alone: it is set just before the lock and back to the default
/// once it is held, so the rest of the transaction waits on its row locks as it did before. A wait that runs out
/// fails with <c>55P03</c> (lock_not_available).
/// </para>
/// </remarks>
public sealed class PostgreSqlTransactionLock : ITransactionLock
{
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

        try
        {
            await database.ExecuteSqlAsync(LockSql(request), cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new TransactionLockTimeoutException(request, exception);
        }
    }

    private static FormattableString LockSql(TransactionLockRequest request)
    {
        var lockFunction = request.Mode == TransactionLockMode.Shared
            ? "pg_advisory_xact_lock_shared"
            : "pg_advisory_xact_lock";
        var (takeLock, keys) = request.Id is { } id
            ? ($"SELECT {lockFunction}({{0}})", new object[] { id })
            : ($"SELECT {lockFunction}({{0}}, hashtext({{1}}))", new object[] { request.Scope, request.Key });

        return request.Timeout is { } timeout
            ? FormattableStringFactory.Create(
                $"SELECT set_config('lock_timeout', {{{keys.Length}}}, true); {takeLock}; SET LOCAL lock_timeout TO DEFAULT",
                [.. keys, LockTimeoutSetting(timeout)])
            : FormattableStringFactory.Create(takeLock, keys);
    }

    // Rounded up to whole milliseconds, as a value under one would read as 0, which turns the timeout off.
    private static string LockTimeoutSetting(TimeSpan timeout) =>
        string.Create(CultureInfo.InvariantCulture, $"{(long)Math.Ceiling(timeout.TotalMilliseconds)}ms");
}
