using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Features.BackgroundJobs;

/// <summary>
/// Registers background job handlers so that running a job builds only the handler of its job type.
/// </summary>
/// <remarks>
/// Each handler is registered twice: keyed by its job type, which is what a job run resolves, and as a plain
/// <see cref="IBackgroundJobHandler"/>, which the host reads once at startup to learn and validate the job types
/// it can run. A handler registered only as a plain <see cref="IBackgroundJobHandler"/> still runs, but every run
/// of its job type then builds every registered handler to find it.
/// </remarks>
public static class BackgroundJobHandlerServiceCollectionExtensions
{
    /// <summary>Registers <typeparamref name="THandler"/>, keyed by the job type of <typeparamref name="TPayload"/>.</summary>
    public static IServiceCollection AddBackgroundJobHandler<THandler, TPayload>(this IServiceCollection services)
        where THandler : BackgroundJobHandler<TPayload>
        where TPayload : IBackgroundJobPayload
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddKeyedScoped<IBackgroundJobHandler, THandler>(TPayload.JobType);
        services.AddScoped<IBackgroundJobHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Registers a handler built by <paramref name="factory"/>, keyed by <paramref name="jobType"/>, for a handler
    /// that implements <see cref="IBackgroundJobHandler"/> directly. The host refuses to start when the handler
    /// declares a job type other than <paramref name="jobType"/>.
    /// </summary>
    public static IServiceCollection AddBackgroundJobHandler(
        this IServiceCollection services,
        string jobType,
        Func<IServiceProvider, IBackgroundJobHandler> factory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentNullException.ThrowIfNull(factory);

        services.AddKeyedScoped(jobType, (provider, _) => factory(provider));
        services.AddScoped(factory);
        return services;
    }
}
