using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Logging;

/// <summary>
/// Provides structured logging capabilities for Endatix setup operations.
/// </summary>
internal class EndatixSetupLogger
{
    private const string SetupOrigin = "Endatix";
    private const string OriginTemplate = "{origin}";
    private readonly ILogger _logger;

    public EndatixSetupLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs an information message during setup with consistent formatting.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    public void Information(string message, params object?[] args)
    {
        object[] logArgs = [SetupOrigin, .. args];
        _logger.LogInformation(OriginTemplate + ": " + message, logArgs);
    }

    /// <summary>
    /// Logs a warning message during setup with consistent formatting.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    public void Warning(string message, params object?[] args)
    {
        object[] logArgs = [SetupOrigin, .. args];
        _logger.LogWarning(OriginTemplate + ": " + message, logArgs);
    }

    /// <summary>
    /// Logs an error message during setup with consistent formatting.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    public void Error(string message, params object?[] args)
    {
        object[] logArgs = [SetupOrigin, .. args];
        _logger.LogError(OriginTemplate + ": " + message, logArgs);
    }
} 