namespace Endatix.IntegrationTests;

/// <summary>
/// Starts a test host the test cannot dispose until the start returns it: when the start fails, it disposes what the
/// start made and rethrows the start's own failure. Every disposal runs, and none of their failures takes the start's
/// place: the start's failure is what the test has to report. A disposal's failure is reported as a warning on the
/// test.
/// </summary>
internal static class FailedHostStart
{
    public static async Task<T> DisposeOnFailureAsync<T>(Func<Task<T>> start, params Func<ValueTask>[] disposals)
    {
        try
        {
            return await start();
        }
        catch (Exception)
        {
            foreach (var dispose in disposals)
            {
                await DisposalFailures.DisposeReportingFailureAsync(dispose);
            }

            throw;
        }
    }
}
