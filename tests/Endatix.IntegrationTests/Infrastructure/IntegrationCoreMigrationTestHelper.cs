using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Infrastructure.Identity;
using Endatix.IntegrationTests.Shared;
using Endatix.Persistence.PostgreSql.Setup;
using Endatix.Persistence.SqlServer.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.IntegrationTests;

/// <summary>
/// Builds a production-like service provider for core DbContext startup migration tests.
/// </summary>
internal static class IntegrationCoreMigrationTestHelper
{
    internal static IServiceProvider BuildServiceProvider(
        string connectionString,
        TestDatabaseProvider provider)
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider.ToString()
            })
            .Build();
        services.AddSingleton(configuration);
        RegisterCoreContextDependencies(services);

        switch (provider)
        {
            case TestDatabaseProvider.PostgreSql:
                services.AddPostgreSqlPersistence<AppDbContext>();
                services.AddPostgreSqlPersistence<AppIdentityDbContext>();
                break;
            case TestDatabaseProvider.SqlServer:
                services.AddSqlServerPersistence<AppDbContext>();
                services.AddSqlServerPersistence<AppIdentityDbContext>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.");
        }

        return services.BuildServiceProvider();
    }

    private static void RegisterCoreContextDependencies(IServiceCollection services)
    {
        services.AddSingleton<IIdGenerator<long>, NoOpIdGenerator>();
        services.AddSingleton<ITenantContext, BypassTenantContext>();
        services.AddSingleton(sp => new EfCoreValueGeneratorFactory(sp.GetRequiredService<IIdGenerator<long>>()));
        services.AddSingleton<OutboxIntegrationEventDispatcher>();
    }

    private sealed class NoOpIdGenerator : IIdGenerator<long>
    {
        public long CreateId() => 0;
    }

    private sealed class BypassTenantContext : ITenantContext
    {
        public long TenantId => 0;
    }
}
