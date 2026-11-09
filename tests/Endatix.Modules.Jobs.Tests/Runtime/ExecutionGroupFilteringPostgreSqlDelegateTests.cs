using Endatix.Modules.Jobs.Runtime;
using Npgsql;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class ExecutionGroupFilteringPostgreSqlDelegateTests
{
    [Fact]
    public void SupportsFireOnAcquire_EndatixDelegate_IsFalse()
    {
        // Arrange — a round without the trigger lock reads its triggers through members of Quartz's own delegates
        // that bypass SelectTriggers, so a recovered trigger would not be given its job type's group, and would not
        // count against that type's cap. Quartz keeps such rounds to the delegates it ships, which this one is not.
        var driverDelegate = new ExecutionGroupFilteringPostgreSqlDelegate();

        // Act
        var supportsFireOnAcquire = driverDelegate.SupportsFireOnAcquire;

        // Assert
        supportsFireOnAcquire.Should().BeFalse();
    }

    [Fact]
    public void PrepareCommand_AcquisitionStatementOutsideAcquisition_Throws()
    {
        // Arrange — a statement carrying the free-slots parameter, prepared without going through acquisition.
        var driverDelegate = new ExecutionGroupFilteringPostgreSqlDelegate();
        using var connection = new NpgsqlConnection();
        var holder = new ConnectionAndTransactionHolder(connection, null);
        const string statement = "SELECT 1 FROM unnest(@endatix_free_groups, @endatix_free_slots)";

        // Act
        var prepare = () => driverDelegate.PrepareCommand(holder, statement);

        // Assert
        prepare.Should().Throw<InvalidOperationException>()
            .WithMessage("*prepared outside trigger acquisition*PostgreSQL driver delegate*");
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

    [Theory]
    [MemberData(nameof(Quartz472AcquisitionSql.Shapes), MemberType = typeof(Quartz472AcquisitionSql))]
    public void GetSelectNextTriggerToAcquireSql_Quartz472_MatchesPinnedSql(
        int maxCount, int excludedJobTypeBucket, string excludedJobTypes)
    {
        // Arrange
        var dialect = new Quartz472AcquisitionSql.PostgreSqlDialect();

        // Act
        var shipped = dialect.AcquisitionSql(maxCount, excludedJobTypeBucket);

        // Assert — the rewrite takes this statement apart; a Quartz upgrade that changes it must update the delegate.
        shipped.Should().Be(Quartz472AcquisitionSql.PostgreSql(maxCount, excludedJobTypes));
    }

    [Theory]
    [MemberData(nameof(Quartz472AcquisitionSql.Shapes), MemberType = typeof(Quartz472AcquisitionSql))]
    public void LimitEachGroupToItsFreeSlots_Quartz472Statement_MatchesPinnedRewrite(
        int maxCount, int _, string excludedJobTypes)
    {
        // Arrange
        var shipped = Quartz472AcquisitionSql.PostgreSql(maxCount, excludedJobTypes);

        // Act
        var rewritten = ExecutionGroupFilteringPostgreSqlDelegate.LimitEachGroupToItsFreeSlots(shipped, maxCount);

        // Assert
        Quartz472RewrittenAcquisitionSql.Normalized(rewritten).Should().Be(
            Quartz472RewrittenAcquisitionSql.PostgreSql(maxCount, excludedJobTypes),
            Quartz472RewrittenAcquisitionSql.RewriteChanged);
    }

    [Theory]
    [MemberData(nameof(Quartz472AcquisitionSql.Shapes), MemberType = typeof(Quartz472AcquisitionSql))]
    public void LimitEachGroupToItsFreeSlots_EveryQuartzShape_KeepsQuartzClauses(
        int maxCount, int excludedJobTypeBucket, string excludedJobTypes)
    {
        // Arrange
        var shipped = new Quartz472AcquisitionSql.PostgreSqlDialect().AcquisitionSql(maxCount, excludedJobTypeBucket);

        // Act
        var rewritten = ExecutionGroupFilteringPostgreSqlDelegate.LimitEachGroupToItsFreeSlots(shipped, maxCount);

        // Assert
        rewritten.Should().Contain(Quartz472AcquisitionSql.PreferredNodeCondition);
        rewritten.Should().Contain("COALESCE(t.execution_group, t.job_name) = free.endatix_group");
        rewritten.Should().EndWith($" LIMIT {maxCount}");
        rewritten.Contains(Quartz472AcquisitionSql.ExclusionCondition(excludedJobTypes), StringComparison.Ordinal)
            .Should().Be(excludedJobTypeBucket > 0);
    }
}
