using OpenFeature.Hosting;

namespace Endatix.Infrastructure.FeatureFlags;

/// <summary>
/// Wraps OpenFeature's lifecycle manager so that shutdown runs at most once per process.
/// </summary>
/// <remarks>
/// <para>
/// <c>OpenFeature.Api</c> is a process-wide singleton, but the hosted lifecycle service that shuts
/// it down is registered per host. <c>Api.ShutdownAsync()</c> completes the SDK's event channel, and
/// a completed channel cannot be reopened, so a second host stopping in the same process throws
/// <c>ChannelClosedException</c> out of <c>IHostedLifecycleService.StoppedAsync</c>.
/// </para>
/// <para>
/// A single application host never notices — there is nothing to shut down twice. Anything that
/// builds more than one host in a process does: the integration test suite creates several
/// <c>WebApplicationFactory</c> instances, and whichever stops second fails on teardown even though
/// the test itself passed.
/// </para>
/// <para>
/// The guard is static because the resource it protects is static. Shutting OpenFeature down is a
/// process-wide, one-way operation, so the second and later calls have nothing left to do.
/// </para>
/// </remarks>
internal sealed class SingleShutdownFeatureLifecycleManager(IFeatureLifecycleManager inner)
    : IFeatureLifecycleManager
{
    private static int _shutdownStarted;

    public ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        inner.EnsureInitializedAsync(cancellationToken);

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _shutdownStarted, 1) == 0
            ? inner.ShutdownAsync(cancellationToken)
            : ValueTask.CompletedTask;

    /// <summary>
    /// Clears the process-wide guard. Exists so tests can exercise the first-shutdown path more
    /// than once; nothing in the application resets it.
    /// </summary>
    internal static void ResetForTests() => Interlocked.Exchange(ref _shutdownStarted, 0);
}
