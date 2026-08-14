using System.Reflection;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs;

/// <summary>
/// Background Jobs module — the durable job queue and, from PR-J3, the runner and sweeper that drain
/// it.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> gated by an <c>IHasFeatureFlag</c>, unlike the Reporting module. Webhook
/// delivery becomes a job type in PR-J4, so from that point the queue is load-bearing for a feature
/// every deployment already has — a flag that could switch it off would switch off webhooks. Whether
/// <em>this process</em> executes jobs is a separate question, answered in PR-J3 by the
/// <c>Endatix:BackgroundJobs:RunInProcess</c> configuration switch; enqueueing and the table are
/// always present.
/// </remarks>
public sealed class JobsModule : IEndatixModule, IHasDbMigrations
{
    public static readonly JobsModule Instance = new();

    private JobsModule() { }

    public Assembly Assembly => typeof(JobsModule).Assembly;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        // Exactly one provider context is registered, and consumers see it only as IJobsDbContext —
        // so nothing downstream branches on the provider.
        if (DatabaseProviderResolver.IsPostgreSql(builder.Configuration))
        {
            builder.AddDbContextWithMigrations<JobsPostgreSqlDbContext>(
                JobsPersistence.ConfigureDbContextOptions);
            builder.Services.AddScoped<IJobsDbContext>(sp =>
                sp.GetRequiredService<JobsPostgreSqlDbContext>());
        }
        else
        {
            builder.AddDbContextWithMigrations<JobsSqlServerDbContext>(
                JobsPersistence.ConfigureDbContextOptions);
            builder.Services.AddScoped<IJobsDbContext>(sp =>
                sp.GetRequiredService<JobsSqlServerDbContext>());
        }

        builder.Services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
    }
}
