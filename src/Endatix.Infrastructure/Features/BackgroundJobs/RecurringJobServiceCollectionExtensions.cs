using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Features.BackgroundJobs;

/// <summary>A worker's declaration of a named UTC cron schedule.</summary>
[Experimental("ENDATIX0008")]
public sealed record RecurringJobSchedule(string Name, string Cron);

/// <summary>Contributes scoped recurring handlers without depending on the optional scheduler.</summary>
[Experimental("ENDATIX0008")]
public static class RecurringJobServiceCollectionExtensions
{
    /// <summary>Registers a UTC cron schedule by its stable, cluster-wide name.</summary>
    public static IServiceCollection AddRecurringJob<T>(this IServiceCollection services, string name, string cron)
        where T : class, IRecurringJob
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(cron);
        if (name.Length > 100 || name != name.Trim())
        {
            throw new ArgumentException("Provide a trimmed schedule name of up to 100 characters.", nameof(name));
        }
        services.AddSingleton(new RecurringJobSchedule(name, cron));
        return services.AddKeyedScoped<IRecurringJob, T>(name);
    }
}
