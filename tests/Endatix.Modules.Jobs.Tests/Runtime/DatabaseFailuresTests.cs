using System.Data.Common;
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

    // EF Core and the scheduler wrap the driver's exception in their own.
    private static InvalidOperationException Wrapped(Exception cause) => new("The write failed.", cause);

    private static PostgresException PostgresFailure(string sqlState) =>
        new("The server refused the command.", "ERROR", "ERROR", sqlState);

    // Another driver's failure, which carries an error code as SqlClient's carries a number.
    private sealed class OtherDriverException(int errorCode) : DbException("The server refused the command.", errorCode);
}
