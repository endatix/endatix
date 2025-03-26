using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Core.Infrastructure.Messaging;
using System.Reflection;
using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Infrastructure.Messaging;

/// <summary>
/// Configuration options for MediatR messaging.
/// </summary>
public class MediatRConfigOptions
{
    /// <summary>
    /// Additional assemblies to scan for handlers.
    /// </summary>
    public Assembly[] AdditionalAssemblies { get; set; } = [];

    /// <summary>
    /// Whether to include logging pipeline behavior.
    /// </summary>
    public bool IncludeLoggingPipeline { get; set; } = true;
}

/// <summary>
/// Provides configuration for MediatR messaging infrastructure.
/// </summary>
public static class MessagingConfiguration
{
    /// <summary>
    /// Adds MediatR messaging services to the service collection.
    /// </summary>
    public static IServiceCollection AddMediatRMessaging(
        this IServiceCollection services,
        Action<MediatRConfigOptions>? configure = null)
    {
        var options = new MediatRConfigOptions();
        configure?.Invoke(options);

        var mediatRAssemblies = new[]
        {
            Endatix.Core.AssemblyReference.Assembly,
            Endatix.Infrastructure.AssemblyReference.Assembly
        };

        if (options.AdditionalAssemblies.Length != 0)
        {
            mediatRAssemblies = [.. mediatRAssemblies, .. options.AdditionalAssemblies];
        }

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(mediatRAssemblies!);
        });

        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        if (options.IncludeLoggingPipeline)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
        }

        return services;
    }
}