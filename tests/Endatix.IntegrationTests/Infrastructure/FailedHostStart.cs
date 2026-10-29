namespace Endatix.IntegrationTests;

/// <summary>
/// Starts a test host the test cannot dispose until the start returns it: when the start fails, it disposes what the
/// start made and rethrows the start's own failure. Every disposal runs, and none of their failures takes the start's
/// place: the start's failure is what the test has to report. A disposal's failure is kept in the start failure's
/// <see cref="Exception.Data"/>.
/// </summary>
internal static class FailedHostStart
{
    public static async Task<T> DisposeOnFailureAsync<T>(Func<Task<T>> start, params Func<ValueTask>[] disposals)
    {
        try
        {
            return await start();
        }
        catch (Exception startFailure)
        {
            foreach (var dispose in disposals)
            {
                await KeepFailureAsync(dispose, startFailure);
            }

            throw;
        }
    }

    public static T DisposeOnFailure<T>(Func<T> start, params Action[] disposals)
    {
        try
        {
            return start();
        }
        catch (Exception startFailure)
        {
            foreach (var dispose in disposals)
            {
                KeepFailure(dispose, startFailure);
            }

            throw;
        }
    }

    private static async Task KeepFailureAsync(Func<ValueTask> dispose, Exception startFailure)
    {
        try
        {
            await dispose();
        }
        catch (Exception disposalFailure)
        {
            Keep(disposalFailure, startFailure);
        }
    }

    private static void KeepFailure(Action dispose, Exception startFailure)
    {
        try
        {
            dispose();
        }
        catch (Exception disposalFailure)
        {
            Keep(disposalFailure, startFailure);
        }
    }

    private static void Keep(Exception disposalFailure, Exception startFailure) =>
        startFailure.Data[$"Disposal failure {startFailure.Data.Count + 1}"] = disposalFailure.ToString();
}
