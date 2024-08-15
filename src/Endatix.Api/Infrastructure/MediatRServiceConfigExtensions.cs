using MediatR;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Infrastructure;

public static class MediatRServiceConfigExtensions
{
    public static void AddMediatRInfrastructure(this IServiceCollection services, Action<MediatRConfigOptions>? options = null)
    {
        var logger = services.CreateLogger("MediatRInfrastructure");
        logger.LogInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Started");
        var meditROptions = new MediatRConfigOptions();
        options?.Invoke(meditROptions);

        var mediatRAssemblies = new[]
        {
            Endatix.Core.AssemblyReference.Assembly
        };

        if (meditROptions.AdditionalAssemblies.Length != 0)
        {
            mediatRAssemblies = [.. mediatRAssemblies, .. meditROptions.AdditionalAssemblies];
        }

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(mediatRAssemblies!));
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        if (meditROptions.IncludeLoggingPipeline)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
            logger.LogInformation("     >> Registering logging pipeline using the {ClassName} class", typeof(LoggingPipelineBehavior<,>).Name);
        }

        logger.LogInformation("{Component} infrastructure configuration | {Status}", "MediatR", "Finished");
    }

    /// <summary>
    /// Using this will register centralized MediatR pipeline logic based of the LoggingPipelineBehavior class
    /// </summary>
    /// <param name="options">MediatRConfigOptions options</param>
    /// <returns>The updated MediatRConfigOptions</returns>
    public static MediatRConfigOptions UsePipelineLogging(this MediatRConfigOptions options)
    {
        options.IncludeLoggingPipeline = true;
        return options;
    }
}
