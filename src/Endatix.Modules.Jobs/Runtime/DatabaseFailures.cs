using System.Collections.Frozen;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Tells a database that is away — unreachable, failing over, stopping, out of connections, timing out, or picking a
/// deadlock victim — from one that answered and refused, by what the driver reports.
/// </summary>
/// <remarks>
/// The driver's own <see cref="DbException.IsTransient"/> is not enough on its own: Microsoft.Data.SqlClient never
/// sets it, and Npgsql does not set it for a statement timeout or for a server that went read-only in a failover. The
/// codes below cover those. No list of codes is complete; a caller that must not mistake an outage for a refusal
/// allows for the codes it misses.
/// </remarks>
internal static class DatabaseFailures
{
    // PostgreSQL SQLSTATEs (Appendix A of its manual) that a database which is away, or will be back, reports.
    private const string PostgresConnectionClass = "08"; // connection_exception and every code in its class
    private static readonly FrozenSet<string> PostgresOutageStates =
    [
        PostgresErrorCodes.SerializationFailure,
        PostgresErrorCodes.DeadlockDetected,
        PostgresErrorCodes.AdminShutdown,
        PostgresErrorCodes.CrashShutdown,
        PostgresErrorCodes.CannotConnectNow,
        PostgresErrorCodes.QueryCanceled, // which a statement_timeout raises
        PostgresErrorCodes.ReadOnlySqlTransaction, // the primary failed over and is now a standby
        PostgresErrorCodes.TooManyConnections,
    ];

    // SQL Server error numbers that a database which is away, or will be back, reports.
    private static readonly FrozenSet<int> SqlServerOutageNumbers =
    [
        -2, // the client's command timeout
        20, // the connection failed in the pre-login handshake
        64, // the network name is no longer available: the connection broke during login
        121, // the semaphore timeout expired: the connection broke
        233, // no process on the other end of the pipe
        1205, // chosen as a deadlock victim
        1222, // lock request timed out
        3906, // the database is read-only: the primary failed over and is now a secondary
        4060, // cannot open the database the login asked for
        4221, // an availability group secondary, recycled, is not yet ready for logins
        10053, // connection aborted by the host
        10054, // connection reset by the peer
        10060, // connection attempt timed out
        10928, // Azure SQL: the database reached a resource limit
        10929, // Azure SQL: the server is too busy to give the database its minimum resources
        40143, // Azure SQL: the service failed to process the request
        40197, // Azure SQL: the service failed to process the request
        40501, // Azure SQL: the service is busy
        40540, // Azure SQL: the service failed to process the request
        40613, // Azure SQL: the database is not currently available
        42108, // Azure SQL serverless: the database is paused
        42109, // Azure SQL serverless: the database is resuming
        49918, // Azure SQL: not enough resources to process the request
        49919, // Azure SQL: too many create or update operations in progress
        49920, // Azure SQL: too many operations in progress
    ];

    /// <summary>
    /// Whether <paramref name="failure"/>, or any exception it wraps, says the database is away rather than that it
    /// refused: a timeout, a failure the driver marks transient, or one whose code is an outage's.
    /// </summary>
    public static bool IsOutage(Exception failure) => failure.HasCause(IsOutageCause);

    /// <summary>
    /// Whether <paramref name="failure"/> came from the database at all: a timeout, or any failure the driver reported.
    /// </summary>
    public static bool IsFromDatabase(Exception failure) => failure.HasCause(cause => cause is DbException or TimeoutException);

    private static bool IsOutageCause(Exception cause) => cause switch
    {
        TimeoutException => true,
        DbException { IsTransient: true } => true,
        PostgresException postgres => IsPostgresOutage(postgres.SqlState),
        SqlException sqlServer => HasSqlServerOutage(sqlServer.Errors),
        _ => false,
    };

    private static bool IsPostgresOutage(string sqlState) =>
        sqlState.StartsWith(PostgresConnectionClass, StringComparison.Ordinal) || PostgresOutageStates.Contains(sqlState);

    // One exception carries every error the batch raised, and the one that tells need not be the first.
    private static bool HasSqlServerOutage(SqlErrorCollection errors) =>
        errors.Cast<SqlError>().Any(error => SqlServerOutageNumbers.Contains(error.Number));
}
