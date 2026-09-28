using Endatix.Core.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IBackgroundJobHandler, ProbeJobHandler>();
    }

    public static string IdList(this IEnumerable<long> ids) => string.Join(',', ids);

    public static string TriggerNameList(this IEnumerable<long> ids) => string.Join(',', ids.Select(id => $"'{id}'"));
}
