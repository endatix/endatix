using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Tests.Shared;

/// <summary>Job type defaults declared the way a module declares them when it registers its handlers.</summary>
internal static class DeclaredDefaults
{
    /// <summary>A job type that sets every value of its own, as webhook delivery does.</summary>
    public static readonly BackgroundJobTypeDefaults Tuned = new()
    {
        MaxAttempts = 8,
        MaxRuntimeMinutes = 5,
        BackoffBaseSeconds = 10,
        BackoffCapSeconds = 3600,
        RetentionDays = 3,
        MaxConcurrency = 4,
    };

    public static JobTypeDefaults None { get; } = new([]);

    /// <summary>Registers <paramref name="defaults"/> for each of <paramref name="jobTypes"/> and reads them back.</summary>
    public static JobTypeDefaults For(BackgroundJobTypeDefaults defaults, params string[] jobTypes)
    {
        var services = new ServiceCollection();
        foreach (var jobType in jobTypes)
        {
            services.AddBackgroundJobTypeDefaults(jobType, defaults);
        }

        using var provider = services.BuildServiceProvider();
        return new JobTypeDefaults(provider.GetServices<BackgroundJobTypeDefaults>());
    }
}
