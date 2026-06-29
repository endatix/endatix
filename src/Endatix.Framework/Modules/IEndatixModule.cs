using System.Reflection;

namespace Endatix.Framework.Modules;

/// <summary>
/// Contract for Endatix platform modules that contribute handlers, endpoints, and persistence.
/// </summary>
public interface IEndatixModule
{
    Assembly Assembly { get; }

    void ConfigureServices(EndatixModuleBuilder builder);
}
