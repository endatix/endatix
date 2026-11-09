using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PostgreSqlUpgrade = Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql.UpgradeQuartzTablesTo472;
using SqlServerUpgrade = Endatix.Modules.Jobs.Persistence.Migrations.SqlServer.UpgradeQuartzTablesTo472;

namespace Endatix.Modules.Jobs.Tests.Persistence;

/// <summary>
/// How the Quartz 4.7.2 upgrade runs. Running nodes write <c>qrtz_TRIGGERS</c> and then <c>qrtz_FIRED_TRIGGERS</c>,
/// and the upgrade alters the tables in another order, so in one migration transaction, which holds every lock to
/// its commit, it could deadlock with a node working through a backlog.
/// </summary>
public sealed class UpgradeQuartzTablesTo472Tests
{
    public static TheoryData<string> Directions => ["Up", "Down"];

    [Theory]
    [MemberData(nameof(Directions))]
    public void Operations_PostgreSql_EachCommitOnTheirOwn(string direction) =>
        OperationsOf(new PostgreSqlUpgrade(), direction).Should().AllSatisfy(CommitsOnItsOwn);

    [Theory]
    [MemberData(nameof(Directions))]
    public void Operations_SqlServer_EachCommitOnTheirOwn(string direction) =>
        OperationsOf(new SqlServerUpgrade(), direction).Should().AllSatisfy(CommitsOnItsOwn);

    private static IReadOnlyList<MigrationOperation> OperationsOf(Migration migration, string direction) =>
        direction == "Up" ? migration.UpOperations : migration.DownOperations;

    private static void CommitsOnItsOwn(MigrationOperation operation) =>
        operation.Should().BeOfType<SqlOperation>().Which.SuppressTransaction.Should().BeTrue();
}
