using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        builder.Services.AddSingleton<IDbContextMigrationContributor>(
            _ => new DbContextMigrationContributor<TContext>(shouldMigrate));

        return builder;
    }
}
