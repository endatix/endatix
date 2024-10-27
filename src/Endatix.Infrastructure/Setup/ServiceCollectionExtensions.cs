using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Endatix.Infrastructure.Setup;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Core.Features.WebHooks;
using Endatix.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions to install Endatix.Infrastructure services
/// For all options check <see cref="ConfigurationOptions"/>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Uses the ILoggerFactory to create new logger instance and derive it from teh ServiceCollection's ServiceProvider
    /// </summary>
    /// <param name="services">IServiceCollection services</param>
    /// <param name="loggerName">Name of the logger</param>
    /// <returns>Logger instance</returns>
    public static ILogger CreateLogger(this IServiceCollection services, string loggerName)
    {
        Guard.Against.NullOrEmpty(services);
        Guard.Against.NullOrEmpty(loggerName);

        var serviceProvider = services.BuildServiceProvider();

        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(loggerName);
        return logger;
    }

    /// <summary>
    /// Add specific Email sender implementation, which will also register configuration for the AppSettings and configure the DI container
    /// </summary>
    /// <typeparam name="TEmailSender">Must implement <c>IEmailSender</c> & <c>IHasInstallLogic</c></typeparam>
    /// <typeparam name="TSettings">The POCO class that will be used for storing the configuration</typeparam>
    /// <param name="services"></param>
    /// <returns>Services ready to follow the AddServices pattern</returns>
    public static IServiceCollection AddEmailSender<TEmailSender, TSettings>(this IServiceCollection services)
           where TEmailSender : class, IEmailSender, IHasConfigSection<TSettings>
           where TSettings : class
    {
        // Configure settings
        services.AddOptions<TSettings>()
                .BindConfiguration($"Email:{typeof(TSettings).Name}")
                .ValidateDataAnnotations();

        // Register the email sender and invoke the initialization delegate
        if (typeof(IPluginInitializer).IsAssignableFrom(typeof(TEmailSender)))
        {
            var initializer = typeof(TEmailSender).GetProperty(nameof(IPluginInitializer.InitializationDelegate))?.GetValue(null) as Action<IServiceCollection>;

            initializer?.Invoke(services);
        }

        services.AddScoped<IEmailSender, TEmailSender>();

        return services;
    }

    /// <summary>
    /// Using this will register centralized MediatR pipeline logic based of the LoggingPipelineBehavior class
    /// </summary>
    /// <param name="options">MediatRConfigOptions options</param>
    /// <returns>The updated MediatRConfigOptions</returns>
    public static MediatRConfigOptions UsePipelineLogging(this MediatRConfigOptions options)
    {
        options.IncludeLoggingPipeline = true;
        return options;
    }

    /// <summary>
    /// This method adds WebHook processing services to the specified <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to configure.</param>
    /// <returns>The configured <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddWebHookProcessing(this IServiceCollection services)
    {
        services.AddSingleton<IBackgroundTasksQueue, BackgroundTasksQueue>();
        services.AddSingleton(typeof(IWebHookService<>), typeof(BackgroundTaskWebHookService<>));
        services.AddHostedService<WebHookBackgroundWorker>();

        services.AddHttpClient<WebHookServer>((serviceProvider, client) =>
               {
                   const int DEFAULT_WEBHOOK_REQUEST_TIMEOUT_IN_SECONDS = 10;
                   client.Timeout = TimeSpan.FromSeconds(DEFAULT_WEBHOOK_REQUEST_TIMEOUT_IN_SECONDS);
                   client.DefaultRequestHeaders.UserAgent.ParseAdd(WebHookRequestHeaders.Constants.ENDATIX_USER_AGENT);
               });

        return services;
    }
}
