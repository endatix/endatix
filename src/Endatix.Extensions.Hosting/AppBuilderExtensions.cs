using Endatix.Setup;
using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Endatix.Setup;

public static class AppBuilderExtensions
{
    public static IApplicationBuilder UseEndatixMiddleware(this WebApplication app)
    {
        app.UseHsts();

        app.UseAuthentication()
            .UseAuthorization();

        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();

        return app;
    }
}
