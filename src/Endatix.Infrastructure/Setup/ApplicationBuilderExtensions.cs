using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Setup;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies database migrations for the AppIdentityDbContext if configured to do so.
    /// </summary>
    /// <param name="app">The IApplicationBuilder instance.</param>
    public static void ApplyDbMigrations(this IApplicationBuilder app)
    {
        var webApp = app as WebApplication;
        Guard.Against.Null(webApp, "The provided IApplicationBuilder is not a WebApplication");

        var dataOptions = webApp.Services
            .GetRequiredService<IOptions<DataOptions>>()
            .Value;

        if (dataOptions.ApplyMigrations)
        {
            using var scope = webApp.Services.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

            if (context.Database.GetPendingMigrations().Any())
            {
                webApp.Logger.LogInformation("💽 Applying database migrations... for {dbContext}", nameof(AppIdentityDbContext));
                context.Database.Migrate();
                webApp.Logger.LogInformation("💽 Database migrations applied for {dbContext}", nameof(AppIdentityDbContext));
            }
        }
    }

    /// <summary>
    /// Seeds an initial admin user account if no users exist in the system. Uses custom credentials from DataOptions if configured,
    /// otherwise falls back to default values.
    /// </summary>
    /// <param name="app">The IApplicationBuilder instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="app"/> is null.</exception>
    public static async void SeedInitialUser(this IApplicationBuilder app)
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