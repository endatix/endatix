using System.Reflection;
using Endatix.Api.Infrastructure;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience;

/// <summary>
/// Audience personalization — per-form properties and people, tenant match key.
/// </summary>
/// <remarks>
/// Gated by <see cref="FeatureFlags.PersonalizationModule"/> (off by default). Owns the
/// <c>audience</c> schema and its migrations. PostgreSQL first.
/// </remarks>
public sealed class AudienceModule : IEndatixModule, IHasFeatureFlag, IHasDbMigrations, IHasFastEndpoints
{
    public static readonly AudienceModule Instance = new();

    private AudienceModule() { }

    public Assembly Assembly => typeof(AudienceModule).Assembly;

    public string FeatureFlag => FeatureFlags.PersonalizationModule;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        RequirePostgreSql(builder.Configuration);
        AddPersistence(builder);
    }

    private static void RequirePostgreSql(IConfiguration configuration)
    {
        if (!DatabaseProviderResolver.IsPostgreSql(configuration))
        {
            throw new InvalidOperationException(
                $"The Audience module requires PostgreSQL. Either set the connection string " +
                $"setting 'DefaultConnection_DbProvider' to 'postgresql', or turn off " +
                $"'Endatix:FeatureFlags:{FeatureFlags.PersonalizationModule}'.");
        }
    }

    private static void AddPersistence(EndatixModuleBuilder builder)
    {
        builder.AddDbContextWithMigrations<AudiencePostgreSqlDbContext>(
            AudiencePersistence.ConfigureDbContextOptions);
        builder.Services.AddScoped<IAudienceDbContext>(
            sp => sp.GetRequiredService<AudiencePostgreSqlDbContext>());
    }
}
