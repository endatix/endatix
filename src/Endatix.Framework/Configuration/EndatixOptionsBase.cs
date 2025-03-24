using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Framework.Configuration;

/// <summary>
/// Base class for all Endatix configuration options.
/// Provides standardized section naming and registration conventions.
/// </summary>
public abstract class EndatixOptionsBase
{
    /// <summary>
    /// The root configuration section name for all Endatix options.
    /// </summary>
    public const string RootSectionName = "Endatix";
    
    /// <summary>
    /// Gets the relative section path for this options class.
    /// Must be implemented by derived classes.
    /// </summary>
    public abstract string SectionPath { get; }
    
    /// <summary>
    /// Gets the full section name including the root.
    /// </summary>
    public string FullSectionName => 
        string.IsNullOrEmpty(SectionPath) ? RootSectionName : $"{RootSectionName}:{SectionPath}";

    /// <summary>
    /// Gets the configuration section path for the specified options type.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <returns>The full section path.</returns>
    public static string GetSectionName<TOptions>() where TOptions : EndatixOptionsBase, new()
    {
        var options = new TOptions();
        return options.FullSectionName;
    }
} 