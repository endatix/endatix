namespace Endatix.IntegrationTests;

/// <summary>
/// Runs a creation whose result the test cannot dispose until the creation returns it, such as a host's start, a
/// migration or a store's setup: when the creation fails, it disposes what the creation built and rethrows the
/// creation's own failure. Every disposal runs, and none of their failures takes the creation's place: the creation's
/// failure is what the test has to report. A disposal's failure is reported as a warning on the test.
/// </summary>
internal static class FailedCreation
{
    public static async Task<T> DisposeOnFailureAsync<T>(Func<Task<T>> create, params Func<ValueTask>[] disposals)
    {
        try
        {
            return await create();
        }
        catch (Exception)
        {
            foreach (var dispose in disposals)
            {
                await DisposalFailures.DisposeReportingFailureAsync("a disposal after the failed creation", dispose);
            }

            throw;
        }
    }
}
