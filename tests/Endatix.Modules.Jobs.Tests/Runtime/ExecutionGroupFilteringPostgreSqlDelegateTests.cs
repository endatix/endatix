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
}
