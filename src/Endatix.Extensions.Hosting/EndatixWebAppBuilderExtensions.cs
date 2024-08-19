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
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Setup;


public static class EndatixHostBuilderExtensions
{
    public static IEndatixApp CreateEndatix(this WebApplicationBuilder builder, Logging.ILogger? logger = null)
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
    public static IEndatixApp AddDefaultSetup(this IEndatixApp endatixApp)
    {
        Guard.Against.Null(endatixApp);

        var builder = endatixApp.WebHostBuilder;

        endatixApp.AddSerilogLogging();

        endatixApp.AddDomainServices();

        endatixApp.AddApplicationMessaging(options =>
        {
            options.UsePipelineLogging();
        });

        endatixApp.AddInfrastructure(configuration => configuration
                                   .AddSecurityServices(options => options
                                       .AddApiAuthentication(builder.Configuration)
                                       .ReadDevUsersFromConfig()
                                   ));

        endatixApp.AddDataPersistence(configuration => configuration
            .WithSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
            .WithCustomTablePrefix()
            .WithSnowflakeIds(0)
            .WithSampleData()
            );

        return endatixApp;
    }
}
