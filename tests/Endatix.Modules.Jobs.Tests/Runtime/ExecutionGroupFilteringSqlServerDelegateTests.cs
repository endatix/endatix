using Endatix.Modules.Jobs.Persistence.Migrations.SqlServer;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Data.SqlClient;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class ExecutionGroupFilteringSqlServerDelegateTests
{
    [Fact]
    public async Task SelectTriggersToAcquire_NoFreeGroup_ReturnsEmpty()
    {
        // Arrange — never initialized and never connected, so reaching the database would throw.
        var driverDelegate = new ExecutionGroupFilteringSqlServerDelegate();
        await using var connection = new SqlConnection();
        var holder = new ConnectionAndTransactionHolder(connection, null);
        var now = DateTimeOffset.UtcNow;
        var criteria = new TriggerAcquisitionCriteria
        {
            NoLaterThan = now,
            NoEarlierThan = now.AddMinutes(-1),
            LiveNodeCutoff = now.AddSeconds(-15),
            MaxCount = 2,
            ExecutionLimits = ExecutionLimitsBuilder.Create().ForGroup("A", 0).ForGroup("B", 0).ForOtherGroups(0).Build(),
        };

        // Act
        var acquired = await driverDelegate.SelectTriggersToAcquire(holder, criteria, TestContext.Current.CancellationToken);

        // Assert
        acquired.Should().BeEmpty();
    }

    [Fact]
    public void PrepareCommand_OutsideAcquisition_Throws()
    {
        // Arrange — a statement carrying the free-slots parameter, prepared without going through acquisition.
        var driverDelegate = new ExecutionGroupFilteringSqlServerDelegate();
        using var connection = new SqlConnection();
        var holder = new ConnectionAndTransactionHolder(connection, null);
        const string statement = "SELECT 1 FROM OPENJSON(@endatix_free_slots)";

        // Act
        var prepare = () => driverDelegate.PrepareCommand(holder, statement);

        // Assert
        prepare.Should().Throw<InvalidOperationException>()
            .WithMessage("*prepared outside trigger acquisition*SQL Server driver delegate*");
    }

    [Theory]
    [InlineData("SELECT TOP 5\r\n t.TRIGGER_NAME FROM {0}TRIGGERS t ORDER BY NEXT_FIRE_TIME ASC")]
    [InlineData("SELECT TOP (5) t.TRIGGER_NAME FROM {0}TRIGGERS t ORDER BY NEXT_FIRE_TIME ASC")]
    [InlineData("SELECT t.TRIGGER_NAME FROM {0}TRIGGERS t ORDER BY NEXT_FIRE_TIME ASC OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY")]
    [InlineData("SELECT TOP 5 t.TRIGGER_NAME FROM {0}TRIGGERS t")]
    public void LimitEachGroupToItsFreeSlots_UnexpectedShape_Throws(string statement)
    {
        // Arrange — statements that do not start with "SELECT TOP 5 " or do not end in an ORDER BY.

        // Act
        var rewrite = () => ExecutionGroupFilteringSqlServerDelegate.LimitEachGroupToItsFreeSlots(statement, 5);

        // Assert
        rewrite.Should().Throw<InvalidOperationException>()
            .WithMessage("*SQL Server driver delegate cannot limit it per execution group*");
    }

    [Theory]
    [MemberData(nameof(Quartz422AcquisitionSql.Shapes), MemberType = typeof(Quartz422AcquisitionSql))]
    public void GetSelectNextTriggerToAcquireSql_Quartz422_MatchesPinnedSql(
        int maxCount, int excludedJobTypeBucket, string excludedJobTypes)
    {
        // Arrange
        var dialect = new Quartz422AcquisitionSql.SqlServerDialect();

        // Act
        var shipped = dialect.AcquisitionSql(maxCount, excludedJobTypeBucket);

        // Assert — the rewrite takes this statement apart; a Quartz upgrade that changes it must update the delegate.
        shipped.Should().Be(Quartz422AcquisitionSql.SqlServer(maxCount, excludedJobTypes));
    }

    [Fact]
    public void LimitEachGroupToItsFreeSlots_AnyShape_ReadsFreeGroupsInTheAcquireColumnsCollation()
    {
        // Arrange — the migration pins the column's collation; the group names read from JSON must share it.
        var collation = $"COLLATE {ExecutionGroupFilteringSqlServerDelegate.AcquireGroupCollation}";

        // Act
        var rewritten = ExecutionGroupFilteringSqlServerDelegate.LimitEachGroupToItsFreeSlots(
            Quartz422AcquisitionSql.SqlServer(11, ""), 11);

        // Assert
        PinTriggerAcquisitionGroupCollation.AddAcquireGroupColumn.Should().Contain($"COALESCE(EXECUTION_GROUP, JOB_NAME) {collation}");
        rewritten.Should().Contain($"endatix_group nvarchar(200) {collation} '$.g'");
    }

    [Theory]
    [MemberData(nameof(Quartz422AcquisitionSql.Shapes), MemberType = typeof(Quartz422AcquisitionSql))]
    public void LimitEachGroupToItsFreeSlots_Quartz422Statement_MatchesPinnedRewrite(
        int maxCount, int _, string excludedJobTypes)
    {
        // Arrange
        var shipped = Quartz422AcquisitionSql.SqlServer(maxCount, excludedJobTypes);

        // Act
        var rewritten = ExecutionGroupFilteringSqlServerDelegate.LimitEachGroupToItsFreeSlots(shipped, maxCount);

        // Assert
        Quartz422RewrittenAcquisitionSql.Normalized(rewritten).Should().Be(
            Quartz422RewrittenAcquisitionSql.SqlServer(maxCount, excludedJobTypes),
            Quartz422RewrittenAcquisitionSql.RewriteChanged);
    }

    [Theory]
    [MemberData(nameof(Quartz422AcquisitionSql.Shapes), MemberType = typeof(Quartz422AcquisitionSql))]
    public void LimitEachGroupToItsFreeSlots_EveryQuartzShape_FiltersOnTheAcquireColumnAndKeepsQuartzClauses(
        int maxCount, int excludedJobTypeBucket, string excludedJobTypes)
    {
        // Arrange
        var shipped = new Quartz422AcquisitionSql.SqlServerDialect().AcquisitionSql(maxCount, excludedJobTypeBucket);

        // Act
        var rewritten = ExecutionGroupFilteringSqlServerDelegate.LimitEachGroupToItsFreeSlots(shipped, maxCount);

        // Assert — the column, not the expression it is computed from, so the acquisition index serves the read.
        rewritten.Should().StartWith($"SELECT TOP ({maxCount}) acquirable.*");
        rewritten.Should().Contain("t.ENDATIX_ACQUIRE_GROUP = free.endatix_group");
        rewritten.Should().NotContainEquivalentOf("COALESCE(");
        rewritten.Should().Contain(Quartz422AcquisitionSql.PreferredNodeCondition);
        rewritten.Contains(Quartz422AcquisitionSql.ExclusionCondition(excludedJobTypes), StringComparison.Ordinal)
            .Should().Be(excludedJobTypeBucket > 0);
    }
}
