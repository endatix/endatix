using System.Reflection;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Framework.Modules;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs;

/// <summary>
/// Background Jobs module — the durable job queue.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> gated by an <c>IHasFeatureFlag</c>, unlike the Reporting module. The queue
/// is shared infrastructure that user-visible features depend on, so a flag able to switch it off
/// would silently switch those features off with it.
/// <para>
/// Whether a given process <em>executes</em> jobs is a separate question, and a configuration one:
/// every host registers the queue and can enqueue, while only hosts configured to run jobs drain it.
/// That is what allows API and worker roles to be deployed separately from the same image.
/// </para>
/// <para>
/// This module registers persistence and enqueueing. It contains no component that executes jobs, so
/// on its own it leaves enqueued rows in <c>Pending</c>.
/// </para>
/// </remarks>
public sealed class JobsModule : IEndatixModule, IHasDbMigrations
{
    public static readonly JobsModule Instance = new();

    private JobsModule() { }

    public Assembly Assembly => typeof(JobsModule).Assembly;

    public void ConfigureServices(EndatixModuleBuilder builder)
    {
        // Only PostgreSQL has a queue implementation. Rather than fail startup — which would take
        // down every SQL Server deployment, including those that never enqueue anything — the
        // interface is still registered, by a stand-in that explains itself at the call site.
        if (!DatabaseProviderResolver.IsPostgreSql(builder.Configuration))
        {
            builder.Services.AddScoped<IBackgroundJobQueue, UnavailableBackgroundJobQueue>();
            return;
        }

        // Consumers see the context only as IJobsDbContext, so nothing downstream branches on the
        // provider.
        builder.AddDbContextWithMigrations<JobsPostgreSqlDbContext>(
            JobsPersistence.ConfigureDbContextOptions);
        builder.Services.AddScoped<IJobsDbContext>(sp =>
            sp.GetRequiredService<JobsPostgreSqlDbContext>());

        builder.Services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
    }
}
