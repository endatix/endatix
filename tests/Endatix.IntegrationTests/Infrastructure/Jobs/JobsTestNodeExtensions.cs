using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

internal static class JobsTestNodeExtensions
{
    public static async Task<IReadOnlyList<long>> EnqueueManyAsync(
        this JobsTestNode node,
        IReadOnlyList<BackgroundJobRequest> requests,
        CancellationToken cancellationToken)
    {
        await using var scope = node.Services.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
        return await queue.EnqueueManyAsync(requests, cancellationToken);
    }

    public static async Task<long> EnqueueAsync(
        this JobsTestNode node,
        BackgroundJobRequest request,
        CancellationToken cancellationToken) =>
        (await node.EnqueueManyAsync([request], cancellationToken))[0];

    public static void AddProbe(this IServiceCollection services, ProbeInvocations invocations)
    {
        services.AddSingleton(invocations);
        services.AddBackgroundJobHandler<ProbeJobHandler, ProbePayload>();
    }

    /// <summary>
    /// Shortens the waits of a job getting past a database outage: its re-fire comes after <paramref name="refireDelay"/>
    /// (half a second unless given), and an outcome write's and a refused trigger store's next tries within
    /// milliseconds. An outcome write still makes <see cref="OutcomeWriteRetries.Default"/>'s number of tries, so a
    /// test's count of failures one firing spends stays right.
    /// </summary>
    /// <remarks>Replaces the module's values, which it adds only when absent, so it works before or after the module.</remarks>
    public static void UseQuickOutageRetries(this IServiceCollection services, TimeSpan? refireDelay = null)
    {
        services.Replace(ServiceDescriptor.Singleton(new RefireDelay(refireDelay ?? TimeSpan.FromMilliseconds(500))));
        services.Replace(ServiceDescriptor.Singleton(new OutcomeWriteRetries(
            OutcomeWriteRetries.Default.Delays.Select(_ => TimeSpan.FromMilliseconds(50)).ToArray())));
        services.Replace(ServiceDescriptor.Singleton(
            new TriggerStoreBackoff(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200))));
    }

    public static string IdList(this IEnumerable<long> ids) => string.Join(',', ids);
}
