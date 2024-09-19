using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Setup;

public static class ApplicationBuilderExtensions
{
    public static void ApplyDbMigrations(this IApplicationBuilder app)
    {
        if (app is not WebApplication webApp)
        {
            throw new ArgumentException("The provided IApplicationBuilder is not a WebApplication", nameof(app));
        }

        var isDevelopment = webApp.Environment.IsDevelopment();
        var applyMigrations = webApp.Configuration.GetValue<bool>("Endatix:ApplyMigrations");

        if (applyMigrations)
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();
            using AppIdentityDbContext context = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

            if (context.Database.GetPendingMigrations().Any())
            {
                webApp.Logger.LogInformation("💽 Applying database migrations... for {dbContext}", nameof(AppIdentityDbContext));
                context.Database.Migrate();
                webApp.Logger.LogInformation("💽 Database migrations applied for {dbContext}", nameof(AppIdentityDbContext));
            }
        }
    }
}