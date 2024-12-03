using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Services;
using Endatix.Framework.Hosting;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Data.Abstractions;
using Endatix.Infrastructure.Email;
using Endatix.Infrastructure.Features.Submissions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Repositories;
using Endatix.Infrastructure.Setup;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

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

        endatixApp.LogSetupInformation("Serilog logging configured");

        return endatixApp;
    }

    public static IEndatixApp AddDomainServices(this IEndatixApp endatixApp)
    {
        endatixApp.Services.AddScoped<IFormService, FormService>();

        return endatixApp;
    }

    /// <summary>
    /// Adds domain services to the specified <see cref="IEndatixApp"/> instance.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <param name="configuration">The configured <see cref="IEndatixApp"/> instance.</param>
    /// <returns></returns>
    public static IEndatixApp AddInfrastructure(this IEndatixApp endatixApp, Action<ConfigurationOptions> configuration)
    {
        var setupSettings = new ConfigurationOptions();
        configuration.Invoke(setupSettings);

        var services = endatixApp.Services;

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IFormsRepository, FormsRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddEmailSender<SendGridEmailSender, SendGridSettings>();
        services.AddWebHookProcessing();

        endatixApp.AddDataOptions();

        endatixApp.AddSubmissionOptions();
        services.AddScoped(typeof(ISubmissionTokenService), typeof(SubmissionTokenService));

        endatixApp.SetupIdentity(setupSettings);

        return endatixApp;
    }

    /// <summary>
    /// Adds infrastructure services to the specified <see cref="IEndatixApp"/> instance, including security and email services, based of specified configuration options.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <param name="options">A delegate to configure the infrastructure options.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    public static IEndatixApp AddApplicationMessaging(this IEndatixApp endatixApp, Action<MediatRConfigOptions>? options = null)
    {
        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Started");

        var meditROptions = new MediatRConfigOptions();
        options?.Invoke(meditROptions);

        var services = endatixApp.Services;
        var mediatRAssemblies = new[]
        {
            Endatix.Core.AssemblyReference.Assembly
        };

        if (meditROptions.AdditionalAssemblies.Length != 0)
        {
            mediatRAssemblies = [.. mediatRAssemblies, .. meditROptions.AdditionalAssemblies];
        }

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(mediatRAssemblies!);
            config.NotificationPublisher = new TaskToThreadPoolPublisher();
            config.NotificationPublisherType = typeof(TaskToThreadPoolPublisher);

        });
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        if (meditROptions.IncludeLoggingPipeline)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
            endatixApp.LogSetupInformation("     >> Registering logging pipeline using the {ClassName} class", typeof(LoggingPipelineBehavior<,>).Name);
        }

        endatixApp.LogSetupInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Finished");

        return endatixApp;
    }

    /// <summary>
    /// Adds data options to the specified <see cref="IEndatixApp"/> instance, based on the configuration options.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    private static IEndatixApp AddDataOptions(this IEndatixApp endatixApp)
    {
        endatixApp.Services.AddOptions<DataOptions>()
            .BindConfiguration(DataOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return endatixApp;
    }

    /// <summary>
    /// Adds submission options to the specified <see cref="IEndatixApp"/> instance, based on the configuration options.
    /// </summary>
    /// <param name="endatixApp">The <see cref="IEndatixApp"/> instance to configure.</param>
    /// <returns>The configured <see cref="IEndatixApp"/> instance.</returns>
    private static IEndatixApp AddSubmissionOptions(this IEndatixApp endatixApp)
    {
        endatixApp.Services.AddOptions<SubmissionOptions>()
            .BindConfiguration(SubmissionOptions.SECTION_NAME)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return endatixApp;
    }
}
