using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Modules.Jobs.Runtime;

internal static class JobStateRepositoryScope
{
    /// <summary>
    /// Runs <paramref name="work"/> against a state repository in a scope of its own, because a <c>DbContext</c> is
    /// neither thread-safe nor meant to be held for the length of a job.
    /// </summary>
    public static async Task<T> WithStateRepositoryAsync<T>(
        this IServiceScopeFactory scopeFactory,
        Func<IBackgroundJobStateRepository, Task<T>> work)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<IBackgroundJobStateRepository>());
    }
}
