using System.Reflection;
using System.Text;
using Ardalis.GuardClauses;

namespace Endatix.Framework.Scripts;

/// <summary>
/// Utility class for reading SQL scripts from embedded resources.
/// </summary>
public static class ScriptReader
{
    /// <summary>
    /// Reads a SQL script from embedded resources.
    /// </summary>
    /// <param name="scriptPath">The path to the SQL script relative to the Scripts folder (e.g., "Functions/export_form_submissions.sql")</param>
    /// <param name="assembly">The assembly containing the embedded resource. If null, uses the calling assembly.</param>
    /// <returns>The SQL script content</returns>
    /// <exception cref="FileNotFoundException">Thrown when the script file is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the script content is invalid</exception>
    public static string ReadSqlScript(string scriptPath, Assembly? assembly = null)
    {
        Guard.Against.NullOrWhiteSpace(scriptPath, nameof(scriptPath));
        
        assembly ??= Assembly.GetCallingAssembly();
        
        // Normalize the path to use dots instead of slashes for resource names
        var resourceName = $"{assembly.GetName().Name}.Scripts.{scriptPath.Replace('/', '.').Replace('\\', '.')}";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream is null)
        {
            var availableResources = assembly.GetManifestResourceNames()
                .Where(name => name.Contains(".Scripts."))
                .ToList();
                
            var resourceList = availableResources.Any() 
                ? string.Join(Environment.NewLine, availableResources.Select(r => $"  - {r}"))
                : "  (no SQL scripts found)";
                
            throw new FileNotFoundException(
                $"SQL script not found: {resourceName}{Environment.NewLine}" +
                $"Available SQL script resources:{Environment.NewLine}{resourceList}");
        }
        
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var content = reader.ReadToEnd();
        
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"SQL script is empty or contains only whitespace: {resourceName}");
        }
        
        return content;
    }
    
    /// <summary>
    /// Gets all available SQL script resource names from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to search. If null, uses the calling assembly.</param>
    /// <returns>Collection of SQL script resource names</returns>
    public static IEnumerable<string> GetAvailableScripts(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        
        return assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Scripts.") && name.EndsWith(".sql"))
            .Select(name => name.Replace($"{assembly.GetName().Name}.Scripts.", "").Replace('.', '/'))
            .OrderBy(name => name);
    }
} 