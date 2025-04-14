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
    /// Configures Endatix services with default settings.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <returns>The host builder for chaining.</returns>
    /// <example>
    /// builder.Host.ConfigureEndatix();
    /// </example>
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
            
            // Finalize all configurations to ensure proper initialization
            builder.FinalizeConfiguration();
        });

        return hostBuilder;
    }

    /// <summary>
    /// Configures Endatix services with custom options.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="configureAction">The action to configure the Endatix builder.</param>
    /// <returns>The host builder for chaining.</returns>
    /// <example>
    /// builder.Host.ConfigureEndatix(endatix => {
    ///     endatix.Infrastructure.Messaging.Configure(options => {
    ///         options.IncludeLoggingPipeline = true;
    ///     });
    ///     endatix.UseSqlServer&lt;AppDbContext&gt;();
    /// });
    /// </example>
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
            
            // Finalize all configurations to ensure proper initialization
            builder.FinalizeConfiguration();
        });
        
        return hostBuilder;
    }
    
    /// <summary>
    /// First applies default settings, then applies custom configuration.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="configureAction">Action to configure Endatix services after defaults.</param>
    /// <returns>The host builder for chaining.</returns>
    /// <example>
    /// builder.Host.ConfigureEndatixWithDefaults(endatix => {
    ///     endatix.Infrastructure.Identity.Configure(options => {
    ///         options.RequireConfirmedEmail = true;
    ///     });
    /// });
    /// </example>
    public static IHostBuilder ConfigureEndatixWithDefaults(
        this IHostBuilder hostBuilder,
        Action<EndatixBuilder> configureAction)
    {
        Guard.Against.Null(hostBuilder);
        Guard.Against.Null(configureAction);

        return hostBuilder.ConfigureEndatix(builder =>
        {
            // First apply defaults
            builder.UseDefaults();

            // Then apply custom configuration
            configureAction(builder);
        });
    }
} 