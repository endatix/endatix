namespace Endatix.IntegrationTests;

/// <summary>
/// Disposes what a test host built without letting a disposal's failure replace a failure that is already on its way
/// to the test. Such a disposal failure is reported as a warning on the running test, which xUnit prints next to the
/// test's outcome. Outside a test, xUnit only sends it as a diagnostic message, which is off by default, so a failure
/// reported there is not shown.
/// </summary>
internal static class DisposalFailures
{
    private const string DatabaseDrop = "the database drop after a failed host disposal";

    /// <summary>
    /// Runs <paramref name="dispose"/>, reporting its failure as <paramref name="what"/> failing instead of throwing it.
    /// </summary>
    public static async ValueTask DisposeReportingFailureAsync(string what, Func<ValueTask> dispose)
    {
        try
        {
            await dispose();
        }
        catch (Exception disposalFailure)
        {
            Report(what, disposalFailure);
        }
    }

    /// <summary>
    /// Disposes a host and then drops the database it ran on, whatever the host's disposal did.
    /// </summary>
    /// <remarks>
    /// A host that does not stop fails its test. When the drop fails too, the host's failure is the one thrown and the
    /// drop's is reported, since a throw from the drop would replace it.
    /// </remarks>
    public static async ValueTask DisposeHostThenDatabaseAsync(IAsyncDisposable host, IAsyncDisposable database)
    {
        try
        {
            await host.DisposeAsync();
        }
        catch (Exception)
        {
            await DisposeReportingFailureAsync(DatabaseDrop, database.DisposeAsync);
            throw;
        }

        await database.DisposeAsync();
    }

    private static void Report(string what, Exception disposalFailure)
    {
        try
        {
            var test = TestContext.Current.Test?.TestDisplayName ?? "outside a test";
            TestContext.Current.AddWarning($"{test}: {what} failed: {disposalFailure}");
        }
        catch (Exception)
        {
            // Reporting runs while another failure is on its way to the test, and a throw here would replace it, so a
            // failure that cannot even be described is left out.
        }
    }
}
