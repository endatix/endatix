namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Every text the job wrapper may store in a job's <c>ErrorMessage</c>.
/// </summary>
/// <remarks>
/// The job status endpoint returns that column verbatim, so its text is author-written and lives here rather than
/// at the call sites, where a caught exception's message is a keystroke away.
/// </remarks>
internal static class BackgroundJobMessages
{
    public const string HandlerThrew = "The job could not be completed.";

    public const string StoppedOnLastAttempt = "The job stopped during its last attempt and has no attempts left.";

    public const string RuntimeCeilingReached = "The job exceeded its maximum run time.";

    /// <summary>Stands in for a failure the handler reported without saying why.</summary>
    public const string FailedWithoutMessage = "The job failed.";
}
