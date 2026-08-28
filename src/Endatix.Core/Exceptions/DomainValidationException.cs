namespace Endatix.Core.Exceptions;

/// <summary>
/// An invalid argument the caller supplied, carrying a message that is safe to return to them.
/// </summary>
/// <remarks>
/// Derives from <see cref="ArgumentException"/> so that existing <c>catch (ArgumentException)</c> sites
/// and tests keep working unchanged; the added <see cref="IEndUserSafeError"/> is what lets a handler
/// surface the reason instead of masking it.
/// </remarks>
public class DomainValidationException : ArgumentException, IEndUserSafeError
{
    /// <param name="message">Author-written text describing the rejection. Safe to show the caller.</param>
    /// <param name="paramName">Internal parameter name. Used for field attribution, never emitted.</param>
    /// <param name="innerException">Optional cause. Its message is never surfaced.</param>
    public DomainValidationException(string message, string? paramName = null, Exception? innerException = null)
        : base(message, paramName, innerException)
    {
        EndUserMessage = message;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Held separately from <see cref="Exception.Message"/> because
    /// <see cref="ArgumentException"/> appends " (Parameter '...')" to it - an internal parameter name
    /// that must not reach the caller.
    /// </remarks>
    public string EndUserMessage { get; }
}
