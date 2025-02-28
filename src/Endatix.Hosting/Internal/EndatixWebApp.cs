using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Endatix.Hosting.Core;

namespace Endatix.Hosting.Internal;

/// <summary>
/// Implementation of the IEndatixApp interface.
/// </summary>
internal class EndatixWebApp : IEndatixApp
{
    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    public ILogger Logger { get; }
    
    /// <summary>
    /// Gets the service collection from the web host builder.
    /// </summary>
    public IServiceCollection Services => WebHostBuilder.Services;
    
    /// <summary>
    /// Gets the web application builder.
    /// </summary>
    public WebApplicationBuilder WebHostBuilder { get; }
    
    /// <summary>
    /// Initializes a new instance of the EndatixWebApp class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="builder">The web application builder.</param>
    public EndatixWebApp(ILogger logger, WebApplicationBuilder builder)
    {
        Logger = logger;
        WebHostBuilder = builder;
    }
    
    /// <summary>
    /// Logs an informational message during application setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    public void LogSetupInformation(string message, params object?[] args)
    {
        Logger.LogInformation(message, args);
    }
    
    /// <summary>
    /// Logs a warning message during application setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    public void LogSetupWarning(string message, params object?[] args)
    {
        Logger.LogWarning(message, args);
    }
    
    /// <summary>
    /// Logs an error message during application setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    public void LogSetupError(string message, params object?[] args)
    {
        Logger.LogError(message, args);
    }
} 