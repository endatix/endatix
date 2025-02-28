using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Endatix.Hosting.Core;

namespace Endatix.Hosting.Internal;

/// <summary>
/// Implementation of the IAppEnvironment interface.
/// </summary>
internal class AppEnvironment : IAppEnvironment
{
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the AppEnvironment class.
    /// </summary>
    /// <param name="environment">The web host environment.</param>
    public AppEnvironment(IWebHostEnvironment environment)
    {
        Guard.Against.Null(environment);
        _environment = environment;
    }

    /// <summary>
    /// Gets the name of the current hosting environment.
    /// </summary>
    public string EnvironmentName => _environment.EnvironmentName;

    /// <summary>
    /// Gets whether the current environment is Development.
    /// </summary>
    /// <returns>True if the environment is Development; otherwise, false.</returns>
    public bool IsDevelopment() => _environment.IsDevelopment();
} 