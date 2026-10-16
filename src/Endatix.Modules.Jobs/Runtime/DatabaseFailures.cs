using System.Collections.Frozen;
using System.Data.Common;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Tells a database that is away — unreachable, failing over, stopping, out of connections, timing out — or busy with
/// contention — a deadlock victim, a lock wait that timed out — from one that answered and refused, by what the
/// driver reports.
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
    private static readonly FrozenSet<string> PostgresAwayStates =
    [
        PostgresErrorCodes.InvalidCatalogName, // the database is missing: being created or restored, or misnamed
        PostgresErrorCodes.AdminShutdown,
        PostgresErrorCodes.CrashShutdown,
        PostgresErrorCodes.CannotConnectNow,
        PostgresErrorCodes.QueryCanceled, // which a statement_timeout raises
        PostgresErrorCodes.ReadOnlySqlTransaction, // the primary failed over and is now a standby
        PostgresErrorCodes.TooManyConnections,
    ];

    // PostgreSQL SQLSTATEs of a database that answered but lost a race with another transaction.
    private static readonly FrozenSet<string> PostgresContentionStates =
    [
        PostgresErrorCodes.SerializationFailure,
        PostgresErrorCodes.DeadlockDetected,
        PostgresErrorCodes.LockNotAvailable, // which a lock_timeout raises
    ];

    // SQL Server error numbers of a database that answered but lost a race with another transaction.
    private const int SqlServerDeadlockVictim = 1205;
    private const int SqlServerCommandTimeout = -2;

    private static readonly FrozenSet<int> SqlServerContentionNumbers =
    [
        SqlServerDeadlockVictim, // chosen as a deadlock victim
        1222, // lock request timed out
    ];

    // SQL Server answers 4060, together with 18456, for a database that is missing, OFFLINE, RESTORING, or in
    // SINGLE_USER use by another session alike, so the pair cannot tell a misnamed database from maintenance; it is
    // waited out as an outage, and the scheduler names the database while it waits.
    private const int CannotOpenDatabase = 4060;

    // SQL Server error numbers that a database which is away, or will be back, reports.
    private static readonly FrozenSet<int> SqlServerAwayNumbers =
    [
        -2, // the client's command timeout
        2, // Windows native SNI: the server was not found
        20, // the connection failed in the pre-login handshake
        40, // Windows native SNI: could not open a connection to the server
        53, // Windows native SNI: the network path was not found
        64, // the network name is no longer available: the connection broke during login
        121, // the semaphore timeout expired: the connection broke
        233, // no process on the other end of the pipe
        258, // the connection attempt timed out: a host that drops packets, or a failover address not yet moved
        3906, // the database is read-only: the primary failed over and is now a secondary
        4060, // cannot open the database the login asked for
        4221, // an availability group secondary, recycled, is not yet ready for logins
        18401, // logins refused while the server runs its upgrade scripts after an update restart
        10053, // connection aborted by the host
        10054, // connection reset by the peer
        10060, // connection attempt timed out
        10061, // Windows native SNI: the server refused the connection, as a stopped or restarting one does
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
    /// Whether <paramref name="failure"/>, or any exception it wraps, says the database is away or busy with
    /// contention rather than that it refused: a timeout, a failure the driver marks transient, or one whose code is an
    /// outage's or contention's. Either is waited out.
    /// </summary>
    public static bool IsOutage(Exception failure) => failure.HasCause(cause => IsAwayCause(cause) || IsContentionCause(cause));

    /// <summary>
    /// Whether <paramref name="failure"/>, or any exception it wraps, says the database answered but lost a race with
    /// another transaction: a deadlock victim, a lock wait that timed out, a serialization failure.
    /// </summary>
    public static bool IsContention(Exception failure) => failure.HasCause(IsContentionCause);

    /// <summary>
    /// Whether <paramref name="failure"/>, or any exception it wraps (a Quartz <c>JobPersistenceException</c>, say), says
    /// this transaction lost a deadlock or a serialization failure to another, so trying it again can succeed: SQL
    /// Server 1205, PostgreSQL 40P01 and 40001. A lock wait that timed out is not: it would wait as long again.
    /// </summary>
    public static bool IsLostRace(Exception failure) => failure.HasCause(cause => cause switch
    {
        PostgresException postgres =>
            postgres.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure,
        SqlException sqlServer => sqlServer.Errors.Cast<SqlError>().Any(error => error.Number == SqlServerDeadlockVictim),
        _ => false,
    });

    /// <summary>
    /// Whether <paramref name="failure"/>, raised while connecting to the database or talking to it, says the database
    /// is away: an outage that is not contention, or a driver failure with a socket failure anywhere under it, which
    /// SqlClient reports without an error number and sometimes wrapped again, in an <see cref="IOException"/> for a
    /// connection reset during the handshake. A refusal, such as a failed login, is not.
    /// </summary>
    /// <remarks>
    /// Only for database calls, and only under a driver's failure: elsewhere a broken socket is as likely an HTTP
    /// endpoint's as the database's.
    /// </remarks>
    public static bool IsUnreachable(Exception failure) =>
        failure.HasCause(IsAwayCause)
        || (failure.HasCause(cause => cause is DbException) && failure.HasCause(cause => cause is SocketException));

    /// <summary>
    /// Whether <paramref name="failure"/> says the database the connection names could not be opened: on SQL Server
    /// missing, offline, being restored, or held in single-user mode by another session, which it reports alike; on
    /// PostgreSQL missing, as while it is still being created or restored.
    /// </summary>
    public static bool IsDatabaseUnavailable(Exception failure) => failure.HasCause(cause => cause switch
    {
        SqlException sqlServer => sqlServer.Errors.Cast<SqlError>().Any(error => error.Number == CannotOpenDatabase),
        PostgresException postgres => postgres.SqlState == PostgresErrorCodes.InvalidCatalogName,
        _ => false,
    });

    /// <summary>
    /// Whether <paramref name="failure"/>, or any exception it wraps, is a wait that ran out: a timeout, SQL Server's
    /// command timeout (-2), or a PostgreSQL statement cancelled by its timeout.
    /// </summary>
    /// <remarks>
    /// Raised while connecting, a timeout means the database is away. Raised once a connection is open, it means the
    /// database answered and the statement waited too long, usually on a lock another transaction holds: contention,
    /// not an outage. Which of the two is the caller's to know, since it knows whether it had connected.
    /// </remarks>
    public static bool IsTimeout(Exception failure) => failure.HasCause(cause => cause switch
    {
        TimeoutException => true,
        SqlException sqlServer => sqlServer.Errors.Cast<SqlError>().Any(error => error.Number == SqlServerCommandTimeout),
        PostgresException postgres => postgres.SqlState == PostgresErrorCodes.QueryCanceled,
        _ => false,
    });

    /// <summary>
    /// Whether <paramref name="failure"/> came from the database at all: a timeout, or any failure the driver reported.
    /// </summary>
    public static bool IsFromDatabase(Exception failure) => failure.HasCause(cause => cause is DbException or TimeoutException);

    private static bool IsAwayCause(Exception cause) => cause switch
    {
        TimeoutException => true,
        DbException { IsTransient: true } when !IsContentionCause(cause) => true,
        PostgresException postgres => IsPostgresAway(postgres.SqlState),
        SqlException sqlServer => HasSqlServerAway(sqlServer.Errors),
        _ => false,
    };

    private static bool IsContentionCause(Exception cause) => cause switch
    {
        PostgresException postgres => PostgresContentionStates.Contains(postgres.SqlState),
        SqlException sqlServer => sqlServer.Errors.Cast<SqlError>().Any(error => SqlServerContentionNumbers.Contains(error.Number)),
        _ => false,
    };

    private static bool IsPostgresAway(string sqlState) =>
        sqlState.StartsWith(PostgresConnectionClass, StringComparison.Ordinal) || PostgresAwayStates.Contains(sqlState);

    // One exception carries every error the batch raised, and the one that tells need not be the first.
    private static bool HasSqlServerAway(SqlErrorCollection errors) =>
        errors.Cast<SqlError>().Any(error => SqlServerAwayNumbers.Contains(error.Number));
}
