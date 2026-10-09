using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Jobs.Persistence.Config.SqlServer;

/// <summary>
/// SQL Server storage for the background job mapping: the native <c>json</c> type (SQL Server 2025 or Azure SQL
/// Database) and bracketed identifiers.
/// </summary>
[ApplyConfigurationFor<JobsSqlServerDbContext>]
internal sealed class BackgroundJobConfigurationSqlServer : BackgroundJobProviderConfiguration
{
    protected override string JsonColumnType => "json";

    protected override string QuoteIdentifier(string name) => $"[{name}]";
}
