using Endatix.Framework.Modules;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Module builder extensions for persistence registration.
/// </summary>
public static class EndatixModuleBuilderExtensions
{
    /// <summary>
    /// Registers a module DbContext with provider-specific migrations and a startup migration contributor.
    /// </summary>
    public static EndatixModuleBuilder AddDbContextWithMigrations<TContext>(
        this EndatixModuleBuilder builder,
        Action<ModuleDbContextOptions> configureDbContext,
        Func<IServiceProvider, bool>? shouldMigrate = null)
        where TContext : DbContext
    {
        builder.Services.AddModuleDbContext<TContext>(builder.Configuration, configureDbContext);
        builder.Services.AddDbContextMigrationContributor<TContext>(shouldMigrate);
        builder.MarkMigrationContributorRegistered();

        return builder;
    }
}
