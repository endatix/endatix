using Ardalis.GuardClauses;
using Endatix.Framework.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Extensions.Hosting;

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

    public ILogger Logger => _logger;

    public WebApplicationBuilder WebHostBuilder => _builder;

    public IServiceCollection Services => _builder.Services;

    public void LogBuilderInformation(string message, params object?[] args)
    {
        object[] logArgs = [BUILDER_LOG_PREFIX, ..args];
        Logger.LogInformation(ORIGIN_TEMPLATE_VARIABLE + ": " + message, logArgs);
    }

    public void LogBuilderError(string message, params object?[] args)
    {
        object[] logArgs = [BUILDER_LOG_PREFIX, ..args];
        Logger.LogError(ORIGIN_TEMPLATE_VARIABLE + ": " + message, logArgs);
    }

    public void LogBuilderWarning(string message, params object?[] args)
    {
        object[] logArgs = [BUILDER_LOG_PREFIX, ..args];
        Logger.LogWarning(ORIGIN_TEMPLATE_VARIABLE + ": " + message, logArgs);
    }
}
