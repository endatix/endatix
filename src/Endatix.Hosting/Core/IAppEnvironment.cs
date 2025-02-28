namespace Endatix.Hosting.Core;

/// <summary>
/// Defines the contract for providing information about the Endatix application environment.
/// </summary>
public interface IAppEnvironment
{
    /// <summary>
    /// Gets whether the current environment is Development.
    /// </summary>
    /// <returns>True if the environment is Development; otherwise, false.</returns>
    bool IsDevelopment();

    /// <summary>
    /// Gets the name of the current hosting environment.
    /// </summary>
    string EnvironmentName { get; }
} 