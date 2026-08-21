using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Jobs.Persistence.Config.SqlServer;

[ApplyConfigurationFor<JobsSqlServerDbContext>]
internal sealed class BackgroundJobConfigurationSqlServer : BackgroundJobProviderConfiguration
{
    protected override string JsonColumnType => "json";

    protected override string QuoteIdentifier(string columnName) => $"[{columnName}]";
}
