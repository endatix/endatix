using Serilog;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Builders;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Setup;

/// <summary>
/// Provides extension methods for configuring various services and infrastructure components in the Endatix application.
/// </summary>
public static class EndatixAppExtensions
{
    /// <summary>
    /// Adds Serilog logging to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddSerilogLogging(this IEndatixApp endatixApp)
    {
        endatixApp.WebHostBuilder.Host.UseSerilog((context, loggerConfig) =>
                loggerConfig.ReadFrom.Configuration(context.Configuration));

        return endatixApp;
    }

    /// <summary>
    /// Adds domain services to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddDomainServices(this IEndatixApp endatixApp)
    {
        return endatixApp;
    }

    /// <summary>
    /// Adds application messaging to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <param name="options">The optional configuration action.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddApplicationMessaging(this IEndatixApp endatixApp, Action<MediatRConfigOptions>? options = null)
    {
        var services = endatixApp.Services;
        var infrastructureBuilder = new InfrastructureBuilder(services, endatixApp.WebHostBuilder.Configuration);

        if (options != null)
        {
            infrastructureBuilder.Messaging.Configure(options);
        }
        else
        {
            infrastructureBuilder.Messaging.UseDefaults();
        }

        return endatixApp;
    }

    /// <summary>
    /// Adds data options to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    private static IEndatixApp AddDataOptions(this IEndatixApp endatixApp)
    {
        endatixApp.Services
            .AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return endatixApp;
    }

    /// <summary>
    /// Adds submission options to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    private static IEndatixApp AddSubmissionOptions(this IEndatixApp endatixApp)
    {
        endatixApp.Services
            .AddOptions<SubmissionOptions>()
            .BindConfiguration(SubmissionOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return endatixApp;
    }
}
