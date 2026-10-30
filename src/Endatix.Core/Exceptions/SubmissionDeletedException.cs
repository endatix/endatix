namespace Endatix.Core.Exceptions;

/// <summary>
/// A save of a submission found its row deleted, on its own or with its form, after the submission was read. The
/// whole save rolled back, its outbox rows with it; the caller treats the submission as not found.
/// </summary>
public sealed class SubmissionDeletedException : Exception
{
    public SubmissionDeletedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
