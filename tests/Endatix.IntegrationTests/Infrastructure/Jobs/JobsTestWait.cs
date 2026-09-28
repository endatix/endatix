using System.Diagnostics;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

internal static class JobsTestWait
{
    /// <summary>Polls <paramref name="condition"/> until it holds or <paramref name="timeout"/> passes.</summary>
    public static async Task<bool> UntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        return await condition();
    }
}
