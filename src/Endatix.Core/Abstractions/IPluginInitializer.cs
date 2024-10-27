using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Core;

/// <summary>
/// Implementations of this interface introduce InitializationDelegate to add custom install logic during add services configuration
/// </summary>
public interface IPluginInitializer
{
     /// <summary>
     /// Initialization delete to implement the install logic exposing IServiceCollection. Example use (services) => { logic here... };
     /// </summary>
     static abstract Action<IServiceCollection> InitializationDelegate { get; }
}
