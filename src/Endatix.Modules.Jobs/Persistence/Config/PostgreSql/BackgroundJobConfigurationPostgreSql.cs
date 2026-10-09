using Endatix.Infrastructure.Data.Config;

namespace Endatix.Modules.Jobs.Persistence.Config.PostgreSql;

/// <summary>
/// PostgreSQL storage for the background job mapping: <c>jsonb</c> payloads and double-quoted identifiers.
/// </summary>
[ApplyConfigurationFor<JobsPostgreSqlDbContext>]
internal sealed class BackgroundJobConfigurationPostgreSql : BackgroundJobProviderConfiguration
{
    protected override string JsonColumnType => "jsonb";

    protected override string QuoteIdentifier(string name) => $"\"{name}\"";
}
