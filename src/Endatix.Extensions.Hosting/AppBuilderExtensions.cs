using System.Diagnostics;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring Endatix-specific middleware in the application pipeline.
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Adds Endatix-specific middleware to the application's request processing pipeline.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> instance to configure.</param>
    /// <returns>An <see cref="IEndatixMiddleware"/> instance representing the configured middleware.</returns>
    public static IEndatixMiddleware UseEndatixMiddleware(this WebApplication app)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<TenantMiddleware>();

        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/healthz"))
                {
                    return LogEventLevel.Verbose;
                }

                return LogEventLevel.Information;
            };
        });

        var middleware = new EndatixMiddleware(app);

        return middleware;
    }

    /// <summary>
    /// Applies database migrations for the AppIdentityDbContext if configured to do so.
    /// </summary>
    /// <param name="app">The IApplicationBuilder instance.</param>
    public static async Task ApplyDbMigrationsAsync(this IApplicationBuilder app)
    {
        var webApp = app as WebApplication;
        Guard.Against.Null(webApp, "The provided IApplicationBuilder is not a WebApplication");

        var dataOptions = webApp.Services
            .GetRequiredService<IOptions<DataOptions>>()
            .Value;

        if (dataOptions.ApplyMigrations)
        {
            using var scope = webApp.Services.CreateScope();

            using var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ApplyMigrationForContextAsync(appDbContext, webApp);

            using var identityDbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            await ApplyMigrationForContextAsync(identityDbContext, webApp);
        }
    }

    private static async Task ApplyMigrationForContextAsync<T>(T dbContext, WebApplication webApp) where T : DbContext
    {
        Guard.Against.Null(dbContext);
        Guard.Against.Null(webApp);

        if (dbContext.Database.GetPendingMigrations().Any())
        {
            var startTime = Stopwatch.GetTimestamp();
            webApp.Logger.LogInformation("💽 Applying database migrations for {dbContextName}", typeof(T).Name);

            await dbContext.Database.MigrateAsync();

            var elapsedTime = Stopwatch.GetElapsedTime(startTime);
            webApp.Logger.LogInformation("💽 Database migrations applied for {dbContextName}. Took: {elapsedTime} ms.", typeof(T).Name, elapsedTime.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Seeds an initial admin user account if no users exist in the system. Uses custom credentials from DataOptions if configured,
    /// otherwise falls back to default values.
    /// </summary>
    /// <param name="app">The IApplicationBuilder instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="app"/> is null.</exception>
    public static async Task SeedInitialUserAsync(this IApplicationBuilder app)
    {
        var webApp = app as WebApplication;
        Guard.Against.Null(webApp, "The provided IApplicationBuilder is not a WebApplication");

        if (EndatixConfig.Configuration.SeedSampleData)
        {
            await using var scope = webApp.Services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var userRegistrationService = scope.ServiceProvider.GetRequiredService<IUserRegistrationService>();
            var dataOptions = scope.ServiceProvider.GetRequiredService<IOptions<DataOptions>>().Value;

            await IdentitySeed.SeedInitialUser(userManager, userRegistrationService, dataOptions, webApp.Logger);
        }
    }
}
