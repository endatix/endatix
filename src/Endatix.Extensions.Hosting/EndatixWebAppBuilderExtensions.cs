using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Builder;
using Logging = Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Endatix.Framework.Hosting;
using Endatix.Extensions.Hosting;
using System.Runtime.Versioning;
using Endatix.Core.Configuration;
using Microsoft.Extensions.Configuration;

namespace Endatix.Setup;


public static class EndatixHostBuilderExtensions
{
    public static IEndatixApp UseEndatix(this WebApplicationBuilder builder, Logging.ILogger? logger = null)
    {
        Guard.Against.Null(builder);

        if (logger == null)
        {
            var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Async(wt => wt.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput: true))
                .CreateLogger();

            logger = new SerilogLoggerFactory(serilogLogger)
                .CreateLogger(nameof(EndatixWebApp));
        }

        var endatixWebApp = new EndatixWebApp(logger, builder);

        endatixWebApp.LogBuilderInformation("Starting Endatix Web Application Host");

        return endatixWebApp;
    }

    [RequiresPreviewFeatures]
    public static IEndatixApp UseDefaultSetup(this IEndatixApp endatixApp)
    {
        Guard.Against.Null(endatixApp);

        var builder = endatixApp.WebHostBuilder;

        endatixApp.UseSerilogLogging();

        endatixApp.UseDomainServices();

        endatixApp.UseApplicationMessaging();

        endatixApp.UseInfrastructure(configuration => configuration
                                   .AddSecurityServices(options => options
                                       .AddApiAuthentication(builder.Configuration)
                                       .ReadDevUsersFromConfig()
                                   ));

        endatixApp.UseSqlServer(configuration => configuration
            .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
            .UseCustomTablePrefix()
            .UseSnowflakeIds(0)
            .UseSampleData()
            );

        return endatixApp;
    }
}
