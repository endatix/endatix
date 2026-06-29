namespace Endatix.Core.Exceptions;

public sealed class DuplicateSubmissionException : Exception
{
    public DuplicateSubmissionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
