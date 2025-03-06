namespace Endatix.Framework.Hosting;

/// <summary>
/// Implementations of this interface provide information about the Endatix app environment
/// </summary>
public interface IAppEnvironment
{
    /// <summary>
    /// True if the environment name is Development, otherwise false. <see cref="https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.hostingenvironmentextensions.isdevelopment"/>
    /// </summary>
    /// <returns></returns>
    public bool IsDevelopment();

    /// <summary>
    /// The name of the current hosting environment
    /// </summary>
    public string EnvironmentName { get; }
}
