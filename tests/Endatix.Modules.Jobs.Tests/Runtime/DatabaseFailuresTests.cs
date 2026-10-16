using System.ComponentModel;
using System.Data.Common;
using System.Net.Sockets;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// Which database failures say the database is away, to be waited out, and which say it answered and refused.
/// </summary>
public sealed class DatabaseFailuresTests
{
    public static TheoryData<Exception, bool> Failures() => new()
    {
        { Wrapped(PostgresFailure(PostgresErrorCodes.UniqueViolation)), false }, // the database answered
        { Wrapped(PostgresFailure(PostgresErrorCodes.DeadlockDetected)), true },
        { Wrapped(PostgresFailure(PostgresErrorCodes.QueryCanceled)), true }, // a statement_timeout
        { Wrapped(PostgresFailure(PostgresErrorCodes.ReadOnlySqlTransaction)), true }, // read-only after a failover
        { Wrapped(PostgresFailure(PostgresErrorCodes.ConnectionFailure)), true },
        { Wrapped(SqlServerExceptions.WithNumbers(2627)), false }, // unique key violation: the database answered
        { Wrapped(SqlServerExceptions.WithNumbers(1205)), true }, // deadlock victim
        { Wrapped(SqlServerExceptions.WithNumbers(-2)), true }, // client timeout
        // Read-only, on purpose or as a secondary after a failover, which SQL Server reports alike: waiting may never fix it.
        { Wrapped(ReadOnlyDatabase()), false },
        { Wrapped(SqlServerExceptions.WithNumbers(42108)), true }, // serverless database paused
        { Wrapped(SqlServerExceptions.WithNumbers(3621, 1205)), true }, // the deadlock is not the batch's first error
        { Wrapped(new OtherDriverException(1205)), false }, // another driver's error code is not SQL Server's
        { Wrapped(new TimeoutException("The pool is exhausted.")), true },
        // A database dropped while a node runs fails a job's outcome write loudly, as it always has.
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidCatalogName)), false },
        { new InvalidOperationException("Unexpected."), false },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void IsOutage_DriverFailure_TellsAnOutageFromARefusal(Exception failure, bool expected)
    {
        // Act
        var outage = DatabaseFailures.IsOutage(failure);

        // Assert
        outage.Should().Be(expected);
    }

    [Fact]
    public void IsFromDatabase_RefusalTheDatabaseMade_ReturnsTrue()
    {
        // Arrange
        var refusal = Wrapped(SqlServerExceptions.WithNumbers(2627));

        // Act
        var fromDatabase = DatabaseFailures.IsFromDatabase(refusal);

        // Assert
        fromDatabase.Should().BeTrue();
    }

    public static TheoryData<Exception, bool> ConnectFailures() => new()
    {
        // SqlClient reports a refused or broken connection with error number 0 and the socket's failure inside.
        { Wrapped(new ConnectFailureException(new SocketException((int)SocketError.ConnectionRefused))), true },
        // A host that drops packets: the connect times out, with the system's own error inside.
        { Wrapped(SqlServerExceptions.WithInner(258, new Win32Exception(110, "Connection timed out"))), true },
        // A reset during the pre-login handshake: number 0, the socket's failure under an IOException.
        {
            Wrapped(SqlServerExceptions.WithInner(
                0, new IOException("Broken pipe", new SocketException((int)SocketError.ConnectionReset)))),
            true
        },
        { Wrapped(SqlServerExceptions.WithNumbers(18401)), true }, // logins refused while upgrade scripts run
        { Wrapped(SqlServerExceptions.WithNumbers(10061)), true }, // Windows native SNI: connection refused
        { Wrapped(SqlServerExceptions.WithNumbers(53)), true }, // Windows native SNI: network path not found
        { Wrapped(SqlServerExceptions.WithNumbers(40)), true }, // Windows native SNI: could not open a connection
        { Wrapped(SqlServerExceptions.WithNumbers(2)), true }, // Windows native SNI: server not found
        { Wrapped(SqlServerExceptions.WithNumbers(18456)), false }, // login failed: the database answered
        { Wrapped(SqlServerExceptions.WithNumbers(4060)), true }, // the database cannot be opened
        // How SQL Server answers for a missing, offline, restoring or single-user database alike: waited out.
        { Wrapped(SqlServerExceptions.WithNumbers(4060, 18456)), true },
        { Wrapped(SqlServerExceptions.WithNumbers(1205)), false }, // a deadlock victim: contention, not an outage
        { Wrapped(SqlServerExceptions.WithNumbers(1222)), false }, // a lock wait timed out: contention, not an outage
        { Wrapped(PostgresFailure(PostgresErrorCodes.DeadlockDetected)), false },
        { Wrapped(PostgresFailure(PostgresErrorCodes.LockNotAvailable)), false },
        // A missing database: only the start waits for one (IsAwayAtStartup); to a running node it is not away.
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidCatalogName)), false },
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidPassword)), false },
        { Wrapped(PostgresFailure(PostgresErrorCodes.ConnectionFailure)), true },
        { Wrapped(new TimeoutException("The pool is exhausted.")), true },
        // A broken socket under anything but a database driver is not the database's.
        { Wrapped(new HttpRequestException("The endpoint is down.", new SocketException((int)SocketError.ConnectionRefused))), false },
    };

    [Theory]
    [MemberData(nameof(ConnectFailures))]
    public void IsUnreachable_ConnectFailure_TellsADatabaseThatIsAwayFromOneThatRefuses(Exception failure, bool expected)
    {
        // Act
        var unreachable = DatabaseFailures.IsUnreachable(failure);

        // Assert
        unreachable.Should().Be(expected);
    }

    public static TheoryData<Exception, bool> Contention() => new()
    {
        { Wrapped(SqlServerExceptions.WithNumbers(1205)), true }, // deadlock victim
        { Wrapped(SqlServerExceptions.WithNumbers(1222)), true }, // lock request timed out
        { Wrapped(PostgresFailure(PostgresErrorCodes.DeadlockDetected)), true },
        { Wrapped(PostgresFailure(PostgresErrorCodes.LockNotAvailable)), true }, // a lock_timeout
        { Wrapped(PostgresFailure(PostgresErrorCodes.SerializationFailure)), true },
        { Wrapped(SqlServerExceptions.WithNumbers(4060)), false }, // away, not contention
        { Wrapped(SqlServerExceptions.WithNumbers(18456)), false }, // refused
        { Wrapped(new TimeoutException("The pool is exhausted.")), false },
    };

    [Theory]
    [MemberData(nameof(Contention))]
    public void IsContention_DriverFailure_TellsALostRaceFromAnOutageOrARefusal(Exception failure, bool expected)
    {
        // Act
        var contention = DatabaseFailures.IsContention(failure);

        // Assert
        contention.Should().Be(expected);
    }

    public static TheoryData<Exception, bool> UnavailableDatabases() => new()
    {
        { Wrapped(SqlServerExceptions.WithNumbers(4060)), true },
        { Wrapped(SqlServerExceptions.WithNumbers(4060, 18456)), true }, // missing, offline or restoring alike
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidCatalogName)), true }, // missing, being created or restored
        { Wrapped(SqlServerExceptions.WithNumbers(18456)), false }, // a login it does not accept
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidPassword)), false },
    };

    [Theory]
    [MemberData(nameof(UnavailableDatabases))]
    public void IsDatabaseUnavailable_ConnectFailure_TellsADatabaseThatCannotBeOpenedOnEitherProvider(
        Exception failure, bool expected)
    {
        // Act
        var unavailable = DatabaseFailures.IsDatabaseUnavailable(failure);

        // Assert
        unavailable.Should().Be(expected);
    }

    public static TheoryData<Exception, bool> AwayAtStartup() => new()
    {
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidCatalogName)), true }, // being created or restored
        { Wrapped(SqlServerExceptions.WithNumbers(4060, 18456)), true },
        { Wrapped(ReadOnlyDatabase()), true }, // the start waits for a restore or a failover to finish
        { Wrapped(new ConnectFailureException(new SocketException((int)SocketError.ConnectionRefused))), true },
        { Wrapped(SqlServerExceptions.WithNumbers(18456)), false },
        { Wrapped(PostgresFailure(PostgresErrorCodes.InvalidPassword)), false },
    };

    [Theory]
    [MemberData(nameof(AwayAtStartup))]
    public void IsAwayAtStartup_ConnectFailure_WaitsForADatabaseThatIsAwayOrNotThereYet(Exception failure, bool expected)
    {
        // Act
        var away = DatabaseFailures.IsAwayAtStartup(failure);

        // Assert
        away.Should().Be(expected);
    }

    public static TheoryData<Exception, bool> SocketFailures() => new()
    {
        { Wrapped(NpgsqlConnectTimeout()), false }, // Npgsql reports its connect timeout with no socket failure
        { Wrapped(SqlServerRefused()), true },
        { Wrapped(SqlServerUnroutedHost()), false }, // SqlClient reports a host that never answers with no socket failure
        { Wrapped(SqlServerExceptions.WithNumbers(-2)), false }, // a command timeout on a live connection
        { Wrapped(new HttpRequestException("The endpoint is down.", new SocketException((int)SocketError.ConnectionRefused))), false },
    };

    [Theory]
    [MemberData(nameof(SocketFailures))]
    public void HasSocketFailure_DriverFailure_TellsANetworkFailureFromALockWait(Exception failure, bool expected)
    {
        // Act
        var socket = DatabaseFailures.HasSocketFailure(failure);

        // Assert
        socket.Should().Be(expected);
    }

    public static TheoryData<Exception, bool> FailuresToConnect() => new()
    {
        { Wrapped(NpgsqlConnectTimeout()), true },
        {
            Wrapped(new NpgsqlException("Failed to connect to 127.0.0.1:1", new SocketException((int)SocketError.ConnectionRefused))),
            true
        },
        { Wrapped(SqlServerUnroutedHost()), true },
        { Wrapped(SqlServerPreLoginTimeout()), true },
        { Wrapped(SqlServerRefused()), true },
        // SqlClient's command timeout has the pre-login timeout's shape, but the connection was open: a lock wait.
        { Wrapped(SqlServerCommandTimeout()), false },
        // Npgsql's command timeout has the connect timeout's shape, but the connection was open: a lock wait.
        {
            Wrapped(new NpgsqlException("Exception while reading from stream", new TimeoutException("Timeout during reading attempt"))),
            false
        },
        { Wrapped(PostgresFailure(PostgresErrorCodes.QueryCanceled)), false },
        { Wrapped(SqlServerExceptions.WithNumbers(-2)), false },
    };

    [Theory]
    [MemberData(nameof(FailuresToConnect))]
    public void FailedToConnect_DriverTimeout_TellsAConnectionThatNeverOpenedFromALockWait(Exception failure, bool expected)
    {
        // Act
        var failedToConnect = DatabaseFailures.FailedToConnect(failure);

        // Assert
        failedToConnect.Should().Be(expected);
    }

    public static TheoryData<Exception, bool> Timeouts() => new()
    {
        { Wrapped(new TimeoutException("The lock was not granted in time.")), true },
        { Wrapped(SqlServerExceptions.WithNumbers(-2)), true }, // the command timeout
        { Wrapped(PostgresFailure(PostgresErrorCodes.QueryCanceled)), true }, // a statement_timeout
        { Wrapped(SqlServerExceptions.WithNumbers(1205)), false },
        { Wrapped(SqlServerExceptions.WithNumbers(18456)), false },
    };

    [Theory]
    [MemberData(nameof(Timeouts))]
    public void IsTimeout_DriverFailure_TellsAWaitThatRanOut(Exception failure, bool expected)
    {
        // Act
        var timeout = DatabaseFailures.IsTimeout(failure);

        // Assert
        timeout.Should().Be(expected);
    }

    [Theory]
    [InlineData(1205)]
    [InlineData(1222)]
    public void IsOutage_SqlServerContention_IsWaitedOut(int number)
    {
        // Arrange — a job whose write lost a race runs again, as it does after an outage.
        var failure = Wrapped(SqlServerExceptions.WithNumbers(number));

        // Act
        var outage = DatabaseFailures.IsOutage(failure);

        // Assert
        outage.Should().BeTrue();
    }

    // EF Core and the scheduler wrap the driver's exception in their own.
    private static InvalidOperationException Wrapped(Exception cause) => new("The write failed.", cause);

    // What SQL Server 2025 answers a write to a database set READ_ONLY, measured with SqlClient 7.0.2.
    private static SqlException ReadOnlyDatabase() =>
        SqlServerExceptions.WithMessage("Failed to update database \"jobs\" because the database is read-only.", 3906);

    // What SqlClient 7.0.2 reports, as read from real connections against SQL Server 2025: a host that never
    // answers, a listener that never answers the pre-login handshake, a refused port, and a command that ran past its
    // timeout on a live connection.
    private static SqlException SqlServerUnroutedHost() =>
        SqlServerExceptions.WithInner(
            258,
            new Win32Exception(258, "The connection attempt timed out."),
            "A network-related or instance-specific error occurred while establishing a connection to SQL Server. (provider: TCP Provider, error: 35 - An internal exception was caught)");

    private static SqlException SqlServerPreLoginTimeout() =>
        SqlServerExceptions.WithInner(
            -2,
            new Win32Exception(258),
            "Connection Timeout Expired.  The timeout period elapsed while attempting to consume the pre-login handshake acknowledgement.");

    private static SqlException SqlServerRefused() =>
        SqlServerExceptions.WithInner(
            0,
            new SocketException(0),
            "A network-related or instance-specific error occurred while establishing a connection to SQL Server. (provider: TCP Provider, error: 35 - An internal exception was caught)");

    private static SqlException SqlServerCommandTimeout() =>
        SqlServerExceptions.WithInner(
            -2,
            new Win32Exception(258),
            "Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.");

    // What Npgsql 10 reports when its connect timeout runs out, as read from a real connection to an address that
    // never answers.
    private static NpgsqlException NpgsqlConnectTimeout() =>
        new("Failed to connect to 10.255.255.1:5432", new TimeoutException("Timeout during connection attempt"));

    private static PostgresException PostgresFailure(string sqlState) =>
        new("The server refused the command.", "ERROR", "ERROR", sqlState);

    // Another driver's failure, which carries an error code as SqlClient's carries a number.
    private sealed class OtherDriverException(int errorCode) : DbException("The server refused the command.", errorCode);

    // A driver's failure to connect, carrying the socket's failure as SqlClient's does.
    private sealed class ConnectFailureException(SocketException socket) : DbException("The server was not found.", socket);
}
