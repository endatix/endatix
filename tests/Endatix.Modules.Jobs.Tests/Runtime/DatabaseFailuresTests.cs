using System.Data.Common;
using System.Reflection;
using Endatix.Modules.Jobs.Runtime;
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
        { Wrapped(SqlServerFailure(2627)), false }, // unique key violation: the database answered
        { Wrapped(SqlServerFailure(1205)), true }, // deadlock victim
        { Wrapped(SqlServerFailure(-2)), true }, // client timeout
        { Wrapped(SqlServerFailure(3906)), true }, // read-only after a failover
        { Wrapped(SqlServerFailure(42108)), true }, // serverless database paused
        { Wrapped(SqlServerFailure(3621, 1205)), true }, // the deadlock is not the batch's first error
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
        var refusal = Wrapped(SqlServerFailure(2627));

        // Act
        var fromDatabase = DatabaseFailures.IsFromDatabase(refusal);

        // Assert
        fromDatabase.Should().BeTrue();
    }

    // EF Core and the scheduler wrap the driver's exception in their own.
    private static InvalidOperationException Wrapped(Exception cause) => new("The write failed.", cause);

    private static PostgresException PostgresFailure(string sqlState) =>
        new("The server refused the command.", "ERROR", "ERROR", sqlState);

    // SqlClient makes its exceptions only internally, from the errors a batch raised, so this calls what it calls.
    private static SqlException SqlServerFailure(params int[] numbers)
    {
        const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var add = typeof(SqlErrorCollection).GetMethod("Add", Internal)!;
        foreach (var number in numbers)
        {
            add.Invoke(errors, [SqlServerError(number)]);
        }

        var create = typeof(SqlException).GetMethod(
            "CreateException", BindingFlags.NonPublic | BindingFlags.Static, [typeof(SqlErrorCollection), typeof(string)])!;
        return (SqlException)create.Invoke(null, [errors, "16.00.1000"])!;
    }

    private static SqlError SqlServerError(int number)
    {
        var constructor = typeof(SqlError).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception)])!;
        return (SqlError)constructor.Invoke([number, (byte)1, (byte)16, "server", "The server refused the command.", "", 1, null]);
    }

    // Another driver's failure, which carries an error code as SqlClient's carries a number.
    private sealed class OtherDriverException(int errorCode) : DbException("The server refused the command.", errorCode);
}
