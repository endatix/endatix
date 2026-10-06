using System.Data.Common;
using Endatix.Modules.Jobs.Runtime;
using Npgsql;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>Which refused trigger stores are the database's, to be tried until they land.</summary>
public sealed class RefusedRefireTests
{
    public static TheoryData<Exception> DatabaseFailures() => new()
    {
        new TimeoutException("The command timed out."),
        new JobPersistenceException("Couldn't store trigger.", new NpgsqlException("Connection refused.")),
        new JobPersistenceException("Couldn't store trigger.", new TimeoutException("The pool is exhausted.")),
        new SchedulerException("Couldn't store trigger.", new JobPersistenceException("Lock failed.", new TestDbException())),
    };

    public static TheoryData<Exception> SchedulerRefusals() => new()
    {
        new JobPersistenceException("The job (endatix.TenantProbe) referenced by the trigger does not exist."),
        new SchedulerException("The trigger will never fire."),
        new JobPersistenceException("Couldn't store trigger because the BLOB couldn't be serialized.", new IOException("Bad data.")),
        new InvalidOperationException("Unexpected."),
    };

    [Theory]
    [MemberData(nameof(DatabaseFailures))]
    public void IsTransient_FailureCausedByTheDatabase_ReturnsTrue(Exception failure)
    {
        // Act
        var transient = RefusedRefire.IsTransient(failure);

        // Assert
        transient.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SchedulerRefusals))]
    public void IsTransient_RefusalOfTheSchedulersOwn_ReturnsFalse(Exception failure)
    {
        // Act
        var transient = RefusedRefire.IsTransient(failure);

        // Assert
        transient.Should().BeFalse();
    }

    private sealed class TestDbException() : DbException("The server closed the connection.");
}
