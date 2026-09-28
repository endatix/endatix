using System.Reflection;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.FeatureFlags;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Jobs.Endpoints;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Jobs;

/// <summary>
/// Background Jobs module — the durable job queue.
/// </summary>
/// <remarks>
/// Gated by <see cref="FeatureFlags.JobsModule"/>, which is off by default. The module owns a
/// DbContext and its own migrations, so registering it on hosts where nothing enqueues would create
/// a schema no code writes to. The flag has to be flipped on in the same release that moves webhook
/// delivery onto the queue — otherwise upgrading hosts lose fan-out with no error to explain it.
/// <para>
/// Whether a given process <em>executes</em> jobs is a separate question, and a configuration one:
/// every host that registers the module can enqueue, while only hosts configured to run jobs drain
/// the queue. That is what allows API and worker roles to be deployed separately from the same image.
/// </para>
/// <para>
/// The job rows live in the <c>jobs</c> schema, and so do the tables of the scheduler that fires them.
/// <c>Endatix:BackgroundJobs:RunInProcess</c> decides whether this host executes jobs or only schedules
/// them.
/// </para>
/// </remarks>
public sealed class JobsModule : IEndatixModule, IHasFeatureFlag, IHasDbMigrations, IHasFastEndpoints
{
    /// <summary>A metrics pipeline exports this module's metrics only once it subscribes to this meter.</summary>
    public const string MeterName = "Endatix.Jobs";

    public static readonly JobsModule Instance = new();

    private JobsModule() { }

    public Assembly Assembly => typeof(JobsModule).Assembly;

    public string FeatureFlag => FeatureFlags.JobsModule;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        // Reaching here means the flag is on, so the host asked for background jobs and has to be
        // told it cannot have them, rather than discovering it when the first enqueue fails.
        if (!DatabaseProviderResolver.IsPostgreSql(builder.Configuration))
        {
            throw new InvalidOperationException(
                $"The Background Jobs module requires PostgreSQL. Either set the connection string " +
                $"setting 'DefaultConnection_DbProvider' to 'postgresql', or turn off " +
                $"'Endatix:FeatureFlags:{FeatureFlags.JobsModule}'.");
        }

        // Consumers see the context only as IJobsDbContext, so nothing downstream branches on the
        // provider.
        builder.AddDbContextWithMigrations<JobsPostgreSqlDbContext>(
            JobsPersistence.ConfigureDbContextOptions);
        builder.Services.AddScoped<IJobsDbContext>(sp =>
            sp.GetRequiredService<JobsPostgreSqlDbContext>());

        builder.Services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();

        builder.Services.AddOptions<BackgroundJobsOptions>()
            .BindConfiguration(BackgroundJobsOptions.SectionName)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<BackgroundJobsOptions>, BackgroundJobsOptionsValidator>());

        builder.Services.AddSingleton(JobHandlerRegistry.Build);
        builder.Services.AddJobsScheduler(builder.Configuration);
        builder.Services.AddHostedService<JobsSchedulerHostedService>();
        builder.Services.AddJobsDashboard(
            builder.Configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions());
    }
}
