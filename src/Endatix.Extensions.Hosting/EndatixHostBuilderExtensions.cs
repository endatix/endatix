using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Logging = Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Endatix.Framework.Hosting;
using Endatix.Extensions.Hosting;
using Endatix.Core.Configuration;
using Endatix.Framework.Setup;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Identity;

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

        builder.Services.AddEndatixFrameworkServices();

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
    /// Adds the default setup configuration with custom DbContext implementations.
    /// </summary>
    public static IEndatixApp AddDefaultSetup<TAppDbContext, TAppIdentityDbContext>(this IEndatixApp endatixApp)
        where TAppDbContext : AppDbContext
        where TAppIdentityDbContext : AppIdentityDbContext
    {
        Guard.Against.Null(endatixApp);

        var builder = endatixApp.WebHostBuilder;

        endatixApp.AddSerilogLogging();

        endatixApp.AddDomainServices();

        endatixApp.AddApplicationMessaging(options =>
        {
            options.UsePipelineLogging();
        });

        var dbProvider = builder.Configuration.GetConnectionString("DefaultConnection_DbProvider")?.ToLowerInvariant() ?? "sqlserver";
        Action<IEndatixConfig> dbConfig = configuration => configuration
            .WithConnectionString(builder.Configuration.GetConnectionString("DefaultConnection"))
            .WithCustomTablePrefix()
            .WithSnowflakeIds(0)
            .WithSampleData();

        switch (dbProvider)
        {
            case "postgresql":
                endatixApp.AddPostgreSqlDataPersistence<TAppDbContext, TAppIdentityDbContext>(dbConfig);
                break;
            case "sqlserver":
                endatixApp.AddSqlServerDataPersistence(dbConfig);
                break;
            default:
                throw new ArgumentException($"Unsupported database provider: {dbProvider}. Supported values are 'sqlserver' and 'postgresql'");
        }

        endatixApp.AddInfrastructure<TAppIdentityDbContext>(configuration => configuration
            .AddSecurityServices(options => options.AddApiAuthentication(builder.Configuration))
        );

        var hostingOptions = endatixApp.WebHostBuilder.Configuration
            .GetSection(HostingOptions.SECTION_NAME)
            .Get<HostingOptions>();

        if (hostingOptions?.IsAzure == true)
        {
            builder.Services.AddApplicationInsightsTelemetry();
        }

        return endatixApp;
    }

    /// <summary>
    /// Adds the default setup configuration using the base OSS DbContext implementations.
    /// </summary>
    public static IEndatixApp AddDefaultSetup(this IEndatixApp endatixApp)
        => endatixApp.AddDefaultSetup<AppDbContext, AppIdentityDbContext>();
}
