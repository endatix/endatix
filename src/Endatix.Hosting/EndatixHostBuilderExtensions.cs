using Ardalis.GuardClauses;
using Endatix.Hosting.Builders;
using Microsoft.Extensions.Hosting;

namespace Endatix.Hosting;

/// <summary>
/// Extension methods for configuring Endatix with IHostBuilder.
/// </summary>
public static class EndatixHostBuilderExtensions
{
    /// <summary>
    /// Configures Endatix services on the host with default settings.
    /// This is the entry point for adding Endatix to your application.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder ConfigureEndatix(this IHostBuilder hostBuilder)
    {
        Guard.Against.Null(hostBuilder);

        // Use a basic configure services action to get the configuration and register Endatix
        hostBuilder.ConfigureServices((context, services) =>
        {
            // Create the Endatix builder
            var builder = services.AddEndatix(context.Configuration);
            
            // Apply defaults
            builder.UseDefaults();
        });

        return hostBuilder;
    }

    /// <summary>
    /// Configures Endatix services on the host with custom options.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="configureAction">The action to configure the Endatix builder.</param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder ConfigureEndatix(this IHostBuilder hostBuilder, Action<EndatixBuilder> configureAction)
    {
        Guard.Against.Null(hostBuilder);
        Guard.Against.Null(configureAction);

        hostBuilder.ConfigureServices((context, services) =>
        {
            // Create the Endatix builder
            var builder = services.AddEndatix(context.Configuration);
            
            // Apply the configuration action
            configureAction(builder);
        });
        
        return hostBuilder;
    }
} 