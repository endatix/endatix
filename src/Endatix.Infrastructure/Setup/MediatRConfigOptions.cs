using System.Reflection;

namespace Endatix.Infrastructure.Setup;

/// <summary>
/// Defines option for how MediatR registration should be done at startup
/// </summary>
public sealed class MediatRConfigOptions
{
    /// <summary>
    /// Defines if the logging pipeline should be included
    /// </summary>
    public bool IncludeLoggingPipeline { internal get; set; }

    /// <summary>
    /// Additional Assemblies to be Registered in the MediatR's known Types
    /// For more info check <see href="https://github.com/jbogard/MediatR/wiki#setup"/>
    /// </summary>
    public Assembly[] AdditionalAssemblies { get; set; } = [];
}
