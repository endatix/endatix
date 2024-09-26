using System.Reflection;

namespace Endatix.Framework;

/// <summary>
/// Extension methods for the Assembly class
/// </summary>
public static class AssemblyExtensions
{
    /// <summary>
    /// Get all Endatix platform assemblies
    /// </summary>
    /// <param name="assembly">The assembly to start from</param>
    /// <returns>A HashSet of Endatix platform assemblies to ensure no duplication of assemblies</returns>
    public static HashSet<Assembly> GetEndatixPlatormAssemblies(this Assembly assembly)
    {
        HashSet<Assembly> endatixAssemblies = [];
        if (assembly == null || !assembly.IsEndatixAssembly())
        {
            return endatixAssemblies;
        }

        // The entry assembly might contain user defined model builder configurations
        var entryAssembly = Assembly.GetEntryAssembly();

        if (entryAssembly != null)
        {
            endatixAssemblies.Add(entryAssembly);
        }

        // Get all Endatix assemblies that are referenced within the project
        var appDomainAssemblies = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Where(assembly => assembly.IsEndatixAssembly())
            .Select(endatixAssemblies.Add);

        return endatixAssemblies;
    }

    /// <summary>
    /// Check if the assembly is an Endatix assembly
    /// </summary>
    /// <param name="assembly">The assembly to check</param>
    /// <returns>True if the assembly is an Endatix assembly, otherwise false</returns>
    private static bool IsEndatixAssembly(this Assembly assembly)
    {
        var assemblyName = assembly?.GetName()?.Name ?? string.Empty;
        return assemblyName.StartsWith("Endantix.");
    }
}