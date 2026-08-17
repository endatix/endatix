using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Jobs.Persistence.Config.PostgreSql;

[ApplyConfigurationFor<JobsPostgreSqlDbContext>]
internal sealed class BackgroundJobConfigurationPostgreSql : BackgroundJobProviderConfiguration
{
    protected override string JsonColumnType => "jsonb";

    protected override string QuoteIdentifier(string columnName) => $"\"{columnName}\"";
}
