using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Framework.Hosting;

public interface IEndatixApp
{
    ILogger Logger { get; }

    IServiceCollection Services { get; }

    WebApplicationBuilder WebHostBuilder { get; }

    void LogBuilderInformation(string message, params object?[] args);

    void LogBuilderError(string message, params object?[] args);

    void LogBuilderWarning(string message, params object?[] args);
}
