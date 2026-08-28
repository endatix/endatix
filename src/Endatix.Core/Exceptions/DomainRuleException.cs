namespace Endatix.Core.Exceptions;

/// <summary>
/// A domain invariant the caller violated, carrying a message that is safe to return to them.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so that existing
/// <c>catch (InvalidOperationException)</c> sites and tests keep working unchanged; the added
/// <see cref="IEndUserSafeError"/> is what lets a handler surface the rule instead of masking it.
/// </remarks>
public class DomainRuleException : InvalidOperationException, IEndUserSafeError
{
    /// <param name="message">Author-written text describing the violated rule. Safe to show the caller.</param>
    /// <param name="innerException">Optional cause. Its message is never surfaced.</param>
    public DomainRuleException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
        EndUserMessage = message;
    }

    /// <inheritdoc />
    public string EndUserMessage { get; }
}
