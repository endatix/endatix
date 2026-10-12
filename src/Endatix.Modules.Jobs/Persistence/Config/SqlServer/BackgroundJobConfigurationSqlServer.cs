using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Jobs.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server storage for the background job mapping: the native <c>json</c> type (SQL Server 2025 or Azure SQL
/// Database), bracketed identifiers, and a binary collation on the dedup columns, since SQL Server's default
/// collation ignores case.
/// </summary>
[ApplyConfigurationFor<JobsSqlServerDbContext>]
internal sealed class BackgroundJobConfigurationSqlServer : BackgroundJobProviderConfiguration
{
    protected override string JsonColumnType => "json";

    protected override string QuoteIdentifier(string name) => $"[{name}]";

    protected override string OrdinalCollation => "Latin1_General_100_BIN2";
}
