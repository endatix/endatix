using System.Reflection;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Features.Forms;
using Endatix.Modules.Audience.Features.Import;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.OnBehalf;
using Endatix.Modules.Audience.Features.Properties;
using Endatix.Modules.Audience.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Audience;

/// <summary>
/// Audience personalization — per-form properties and people, tenant match key.
/// </summary>
/// <remarks>
/// Gated by <see cref="FeatureFlags.PersonalizationModule"/> (off by default). Owns the
/// <c>audience</c> schema and its migrations, on PostgreSQL or SQL Server.
/// </remarks>
public sealed class AudienceModule : IEndatixModule, IHasFeatureFlag, IHasDbMigrations, IHasFastEndpoints
{
    public static readonly AudienceModule Instance = new();

    private AudienceModule() { }

    public Assembly Assembly => typeof(AudienceModule).Assembly;

    public string FeatureFlag => FeatureFlags.PersonalizationModule;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        AddPersistence(builder);
        builder.Services.AddScoped<IOutboxIntegrationEventHandler, DeleteFormAudienceOutboxHandler>();
        builder.Services.AddScoped<ImportWriteSupport>();
        builder.Services.AddScoped<ISubmissionPersonalizer, OnBehalfPersonalizer>();
        builder.Services.AddScoped<IAudiencePropertyCatalog, AudiencePropertyCatalog>();
        builder.Services.AddScoped<LinkOpenLock>();
        builder.Services.AddScoped<LinkOpenGates>();
    }

    private static void AddPersistence(EndatixModuleBuilder builder)
    {
        if (DatabaseProviderResolver.IsPostgreSql(builder.Configuration))
        {
            AddContext<AudiencePostgreSqlDbContext>(builder);
        }
        else
        {
            AddContext<AudienceSqlServerDbContext>(builder);
        }

        // Consumers see the context only as IAudienceDbContext or the base, so nothing downstream branches on the
        // provider; the base registration is the one line that names it.
        builder.Services.AddScoped<IAudienceDbContext>(sp => sp.GetRequiredService<AudienceDbContextBase>());
        builder.Services.AddSingleton<MatchKeyLock>();
    }

    private static void AddContext<TContext>(EndatixModuleBuilder builder)
        where TContext : AudienceDbContextBase
    {
        builder.AddDbContextWithMigrations<TContext>(AudiencePersistence.ConfigureDbContextOptions);
        builder.Services.AddScoped<AudienceDbContextBase>(sp => sp.GetRequiredService<TContext>());
    }
}
