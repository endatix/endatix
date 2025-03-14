using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Framework.Configuration;

/// <summary>
/// Extension methods for registering Endatix options.
/// </summary>
public static class EndatixOptionsExtensions
{
    /// <summary>
    /// Adds Endatix options to the service collection.
    /// </summary>
    /// <typeparam name="TOptions">The options type, which must inherit from EndatixOptionsBase.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndatixOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TOptions : EndatixOptionsBase, new()
    {
        var sectionName = EndatixOptionsBase.GetSectionName<TOptions>();
        services.Configure<TOptions>(configuration.GetSection(sectionName));
        return services;
    }
    
    /// <summary>
    /// Registers all standard Endatix options types.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStandardEndatixOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register root options
        services.AddEndatixOptions<EndatixRootOptions>(configuration);
        
        // Register standard feature options
        services.AddEndatixOptions<HostingOptions>(configuration);
        
        // Note: The other options classes (DataOptions, SecurityOptions, SubmissionOptions)
        // should be registered by their respective modules to avoid circular dependencies
        
        return services;
    }
} 