using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Framework.Hosting;

/// <summary>
/// Defines properties that must be exposed by root builders to their child builders.
/// This interface allows for type-safe dependencies between builders without circular references.
/// </summary>
public interface IBuilderRoot
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }
    
    /// <summary>
    /// Gets the configuration.
    /// </summary>
    IConfiguration Configuration { get; }
    
    /// <summary>
    /// Gets the application environment.
    /// </summary>
    IAppEnvironment? AppEnvironment { get; }
    
    /// <summary>
    /// Gets the logger factory. All builder roots must provide a logger factory
    /// to ensure proper diagnostic capabilities during application setup.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }
} 