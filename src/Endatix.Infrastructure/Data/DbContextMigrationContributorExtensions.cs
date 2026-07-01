using Endatix.Framework.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// DI extensions for DbContext migration contributors.
/// </summary>
public static class DbContextMigrationContributorExtensions
{
    /// <summary>
    /// Registers a startup migration contributor for the given DbContext type.
    /// Module authors should use <see cref="EndatixModuleBuilderExtensions.AddDbContextWithMigrations{TContext}"/> instead.
    /// </summary>
    internal static IServiceCollection AddDbContextMigrationContributor<TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, bool>? shouldMigrate = null)
        where TContext : DbContext
    {
        services.AddSingleton<IDbContextMigrationContributor>(
            _ => new DbContextMigrationContributor<TContext>(shouldMigrate));

        return services;
    }
}
