using System.ComponentModel;
using System.Data.Common;
using System.Net.Sockets;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
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
        { Wrapped(SqlServerExceptions.WithNumbers(3906)), true }, // read-only after a failover
        { Wrapped(SqlServerExceptions.WithNumbers(42108)), true }, // serverless database paused
        { Wrapped(SqlServerExceptions.WithNumbers(3621, 1205)), true }, // the deadlock is not the batch's first error
        { Wrapped(new OtherDriverException(1205)), false }, // another driver's error code is not SQL Server's
        { Wrapped(new TimeoutException("The pool is exhausted.")), true },
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

    private static PostgresException PostgresFailure(string sqlState) =>
        new("The server refused the command.", "ERROR", "ERROR", sqlState);

    // Another driver's failure, which carries an error code as SqlClient's carries a number.
    private sealed class OtherDriverException(int errorCode) : DbException("The server refused the command.", errorCode);

    // A driver's failure to connect, carrying the socket's failure as SqlClient's does.
    private sealed class ConnectFailureException(SocketException socket) : DbException("The server was not found.", socket);
}
