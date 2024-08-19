using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Framework.Hosting;

/// <summary>
/// Implementation of this interface will be used for setting up the Endatix application
/// during the WebApplicationBuilder initialization. This interface provides
/// properties for accessing services, logging, and the web host builder,
/// as well as methods for logging information, warnings, and errors during the setup process.
/// </summary>
public interface IEndatixApp
{
    /// <summary>
    /// Gets the <see cref="ILogger"/> instance used for logging within the application setup.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> instance used for services configuration
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the <see cref="WebApplicationBuilder"/> instance used to configure and build the application's web host.
    /// </summary>
    WebApplicationBuilder WebHostBuilder { get; }

    /// <summary>
    /// Formatted wrapper on top of <see cref="logger.LogInformation"/>. Logs an informational message during the application setup.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional arguments to format the message.</param>
    void LogSetupInformation(string message, params object?[] args);

    /// <summary>
    /// Formatted wrapper on top of <see cref="logger.LogWarning"/>. Logs a warning message during the application setup.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional arguments to format the message.</param>
    void LogSetupWarning(string message, params object?[] args);

    /// <summary>
    /// Formatted wrapper on top of <see cref="logger.LogError"/>. Logs an error message during the application setup.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional arguments to format the message.</param>
    void LogSetupError(string message, params object?[] args);
}
