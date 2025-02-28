using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Core;

/// <summary>
/// Defines the contract for an Endatix application during setup.
/// Provides access to services, logging, and the web host builder.
/// </summary>
public interface IEndatixApp
{
    /// <summary>
    /// Gets the logger instance used for logging within the application setup.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Gets the service collection used for services configuration.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the WebApplicationBuilder instance used to configure and build the application.
    /// </summary>
    WebApplicationBuilder WebHostBuilder { get; }

    /// <summary>
    /// Logs an informational message during application setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    void LogSetupInformation(string message, params object?[] args);

    /// <summary>
    /// Logs a warning message during application setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    void LogSetupWarning(string message, params object?[] args);

    /// <summary>
    /// Logs an error message during application setup.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    void LogSetupError(string message, params object?[] args);
} 