using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Audience.Tests.Persistence;

/// <summary>
/// The contexts are configured the way the host configures them, so they discover migrations and the
/// snapshot exactly as startup does. No connection is opened.
/// </summary>
internal static class NotConnectedAudienceContexts
{
    public static AudiencePostgreSqlDbContext CreatePostgreSql() =>
        new(
            HostOptions<AudiencePostgreSqlDbContext>(
                "postgresql",
                "Host=127.0.0.1;Database=not_connected;Username=postgres;Password=not-connected"),
            Substitute.For<ITenantContext>());

    public static AudienceSqlServerDbContext CreateSqlServer() =>
        new(
            HostOptions<AudienceSqlServerDbContext>(
                "sqlserver",
                "Server=127.0.0.1;Database=not_connected;User Id=sa;Password=not-connected"),
            Substitute.For<ITenantContext>());

    private static DbContextOptions<TContext> HostOptions<TContext>(string provider, string connectionString)
        where TContext : DbContext
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.ConfigureModuleDbContext(configuration, AudiencePersistence.ConfigureDbContextOptions);
        return optionsBuilder.Options;
    }
}
