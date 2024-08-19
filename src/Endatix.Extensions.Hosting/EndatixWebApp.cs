using Ardalis.GuardClauses;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Extensions.Hosting;

/// <summary>
/// Implementation of the <see cref="IEndatixApp"/>. Wrapper class used for easy setup of the Endatix application
/// </summary>
internal class EndatixWebApp : IEndatixApp
{
    const string BUILDER_LOG_PREFIX = "Endatix";

    private const string ORIGIN_TEMPLATE_VARIABLE = "{origin}";

    private readonly ILogger _logger;

    private readonly WebApplicationBuilder _builder;

    internal EndatixWebApp(ILogger logger, WebApplicationBuilder builder)
    {
        Guard.Against.Null(logger);
        Guard.Against.Null(builder);
        Guard.Against.Null(builder.Services);

        _logger = logger;
        _builder = builder;
    }

    /// <inheritdoc/>
    public ILogger Logger => _logger;

    /// <inheritdoc/>
    public WebApplicationBuilder WebHostBuilder => _builder;

    /// <inheritdoc/>
    public IServiceCollection Services => _builder.Services;

    /// <inheritdoc/>
    public void LogSetupInformation(string message, params object?[] args)
    {
        object[] logArgs = [BUILDER_LOG_PREFIX, .. args];
        Logger.LogInformation(ORIGIN_TEMPLATE_VARIABLE + ": " + message, logArgs);
    }

    /// <inheritdoc/>
    public void LogSetupWarning(string message, params object?[] args)
    {
        object[] logArgs = [BUILDER_LOG_PREFIX, .. args];
        Logger.LogWarning(ORIGIN_TEMPLATE_VARIABLE + ": " + message, logArgs);
    }

    /// <inheritdoc/>
    public void LogSetupError(string message, params object?[] args)
    {
        object[] logArgs = [BUILDER_LOG_PREFIX, .. args];
        Logger.LogError(ORIGIN_TEMPLATE_VARIABLE + ": " + message, logArgs);
    }
}
