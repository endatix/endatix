namespace Endatix.IntegrationTests;

/// <summary>
/// Reports a disposal failure that must not be thrown, because another failure is already on its way to the test, as a
/// warning on the running test. xUnit prints the warning next to the test's outcome. Outside a test, xUnit only sends it
/// as a diagnostic message, which is off by default, so a failure reported there is not shown.
/// </summary>
internal static class DisposalFailures
{
    /// <summary>Runs <paramref name="dispose"/>, reporting its failure instead of throwing it.</summary>
    public static async Task DisposeReportingFailureAsync(Func<ValueTask> dispose)
    {
        try
        {
            await dispose();
        }
        catch (Exception disposalFailure)
        {
            Report(disposalFailure);
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
            await DisposeReportingFailureAsync(database.DisposeAsync);
            throw;
        }

        await database.DisposeAsync();
    }

    public static void Report(Exception disposalFailure)
    {
        try
        {
            var test = TestContext.Current.Test?.TestDisplayName ?? "outside a test";
            TestContext.Current.AddWarning($"{test}: a test host's disposal failed: {disposalFailure}");
        }
        catch (Exception)
        {
            // Reporting runs while another failure is on its way to the test, and a throw here would replace it, so a
            // failure that cannot even be described is left out.
        }
    }
}
