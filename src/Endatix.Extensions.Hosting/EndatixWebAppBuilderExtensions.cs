using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Builder;
using Logging = Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Endatix.Framework.Hosting;
using Endatix.Extensions.Hosting;
using Endatix.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring and setting up the Endatix application within a <see cref="WebApplicationBuilder"/> context as well as  <see cref="IEndatixApp"/> context.
/// </summary>
public static class EndatixHostBuilderExtensions
{
    /// <summary>
    /// Creates and configures an instance of <see cref="IEndatixApp"/> using the provided <see cref="WebApplicationBuilder"/>. This method also configures logging using the provided logger or a default Serilog configuration if none is provided.
    /// </summary>
    /// <param name="builder">The <see cref="WebApplicationBuilder"/> used to configure the application.</param>
    /// <param name="logger">Optional. The <see cref="Logging.ILogger"/> instance for logging. If null, a default Serilog logger is used.</param>
    /// <returns>An instance of <see cref="IEndatixApp"/> representing the configured application.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="builder"/> is null.</exception>
    public static IEndatixApp CreateEndatix(this WebApplicationBuilder builder, Logging.ILogger? logger = null)
    {
        Guard.Against.Null(builder);

        logger ??= CreateSerilogLogger();
        var endatixWebApp = new EndatixWebApp(logger, builder);

        endatixWebApp.LogSetupInformation("Starting Endatix Web Application Host");

        return endatixWebApp;
    }

    /// <summary>
    /// Creates the Serilog Logger to be used during the setup of the application + for setting it as the default Logger for the Host.
    /// </summary>
    /// <returns>the <see cref="ILogger"/>to be used for the Endatix application setup</returns>
    private static Logging.ILogger CreateSerilogLogger()
    {
        Logging.ILogger? logger;
        var serilogLogger = new LoggerConfiguration()
                        .Enrich.FromLogContext()
                        .WriteTo.Async(wt => wt.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Sixteen, applyThemeToRedirectedOutput: true))
                        .CreateLogger();

        logger = new SerilogLoggerFactory(serilogLogger)
            .CreateLogger(nameof(EndatixWebApp));
        return logger;
    }

    /// <summary>
    /// Adds the default setup configuration to the specified <see cref="IEndatixApp"/> instance.This includes adding logging, domain services, application messaging, infrastructure, and data persistence components to the application.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="endatixApp"/> is null.</exception>
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
