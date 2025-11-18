using System.Collections.Immutable;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Endatix.Core.Features.Email;
using Endatix.Core.Features.WebHooks;
using Endatix.Infrastructure.Email;
using Endatix.Infrastructure.Features.WebHooks;
using Endatix.Infrastructure.Setup;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions to install Endatix.Infrastructure services
/// For all options check <see cref="ConfigurationOptions"/>
/// </summary>
public static class ServiceCollectionExtensions
{
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
                .BindConfiguration($"Endatix:Integrations:Email:{typeof(TSettings).Name}")
                .ValidateOnStart();

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
    /// Adds email template settings configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddEmailTemplateSettings(this IServiceCollection services)
    {
        services.AddOptions<EmailTemplateSettings>()
                .BindConfiguration("Endatix:EmailTemplates")
                .ValidateOnStart();

        services.AddScoped<IEmailTemplateService, EmailTemplateService>();

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
        services.AddOptions<WebHookSettings>()
               .BindConfiguration("Endatix:WebHooks")
               .ValidateDataAnnotations();
        services.AddSingleton<IBackgroundTasksQueue, BackgroundTasksQueue>();
        services.AddScoped(typeof(IWebHookService), typeof(BackgroundTaskWebHookService));
        services.AddHostedService<WebHookBackgroundWorker>();
        services.AddHttpClient<WebHookServer>((serviceProvider, client) =>
               {
                   var webHookSettings = serviceProvider.GetRequiredService<IOptions<WebHookSettings>>().Value;

                   client.Timeout = TimeSpan.FromSeconds(webHookSettings.ServerSettings.PipelineTimeoutInSeconds);
                   client.DefaultRequestHeaders.UserAgent.ParseAdd(WebHookRequestHeaders.Constants.ENDATIX_USER_AGENT);
               })
               .AddResilienceHandler("webhook-resilience", static (builder, context) =>
               {
                   var webHookSettings = context.ServiceProvider.GetRequiredService<IOptions<WebHookSettings>>().Value;

                   var exceptionsToHandle = new[]
                    {
                        typeof(TimeoutRejectedException),
                        typeof(SocketException),
                        typeof(HttpRequestException),
                    }.ToImmutableArray();

                   builder.AddRetry(new HttpRetryStrategyOptions
                   {
                       BackoffType = DelayBackoffType.Exponential,
                       Delay = TimeSpan.FromSeconds(webHookSettings.ServerSettings.Delay),
                       MaxRetryAttempts = webHookSettings.ServerSettings.RetryAttempts,
                       UseJitter = true,
                       ShouldHandle = ex => new ValueTask<bool>(exceptionsToHandle.Contains(ex.GetType()) || ex.Outcome.Result?.IsSuccessStatusCode == false)
                   });
                   builder.AddTimeout(TimeSpan.FromSeconds(webHookSettings.ServerSettings.AttemptTimeoutInSeconds));
                   builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
                   {
                       PermitLimit = webHookSettings.ServerSettings.MaxConcurrentRequests,
                       QueueLimit = webHookSettings.ServerSettings.MaxQueueSize
                   });
               });

        return services;
    }
}
