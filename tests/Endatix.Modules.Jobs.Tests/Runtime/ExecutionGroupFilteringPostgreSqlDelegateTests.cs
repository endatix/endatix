using Endatix.Modules.Jobs.Runtime;
using Npgsql;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class ExecutionGroupFilteringPostgreSqlDelegateTests
{
    [Fact]
    public void PrepareCommand_AcquisitionStatementOutsideAcquisition_Throws()
    {
        // Arrange — a statement carrying the free-groups parameter, prepared without going through acquisition.
        var driverDelegate = new ExecutionGroupFilteringPostgreSqlDelegate();
        using var connection = new NpgsqlConnection();
        var holder = new ConnectionAndTransactionHolder(connection, null);
        const string statement = "SELECT 1 WHERE 'group' = ANY(@endatix_free_groups)";

        // Act
        var prepare = () => driverDelegate.PrepareCommand(holder, statement);

        // Assert
        prepare.Should().Throw<InvalidOperationException>().WithMessage("*prepared outside trigger acquisition*");
    }

    [Fact]
    public void LimitEachGroupToItsFreeSlots_StatementWithoutTheBatchLimit_Throws()
    {
        // Arrange — a statement shaped unlike the scheduler's acquisition query.
        const string statement = "SELECT t.trigger_name FROM jobs.qrtz_triggers t ORDER BY t.next_fire_time";

        // Act
        var rewrite = () => ExecutionGroupFilteringPostgreSqlDelegate.LimitEachGroupToItsFreeSlots(statement, 5);

        // Assert
        rewrite.Should().Throw<InvalidOperationException>().WithMessage("*cannot limit it per execution group*");
    }
}
