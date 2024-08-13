using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Identity.Extensions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        using AppIdentityDbContext context = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();

        context.Database.Migrate();
    }
}
