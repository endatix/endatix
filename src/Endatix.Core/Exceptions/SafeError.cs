using Microsoft.Extensions.Logging;

namespace Endatix.Core.Exceptions;

/// <summary>
/// The single sanctioned gate between a caught exception and client-visible error text.
/// </summary>
/// <remarks>
/// A call here is self-documenting: it says the emitted detail is intentional, and it can only ever emit
/// a message a type opted into via <see cref="IEndUserSafeError"/>. Anything else - a BCL argument guard,
/// an EF Core translation failure, an Npgsql error - falls through to the caller's fallback, so widening
/// a <c>catch</c> cannot introduce a leak.
/// </remarks>
public static class SafeError
{
    /// <summary>
    /// Returns <paramref name="exception"/>'s author-written message when its type opted in via
    /// <see cref="IEndUserSafeError"/>; otherwise <paramref name="fallback"/>.
    /// </summary>
    /// <param name="exception">The caught exception. May be <see langword="null"/>.</param>
    /// <param name="fallback">Author-written text to use when the exception did not opt in.</param>
    public static string MessageOr(Exception? exception, string fallback) =>
        exception is IEndUserSafeError safeError && !string.IsNullOrWhiteSpace(safeError.EndUserMessage)
            ? safeError.EndUserMessage
            : fallback;

    /// <summary>
    /// <see cref="MessageOr"/> plus the severity the opt-in implies: an
    /// <see cref="IEndUserSafeError"/> is the domain working as designed, so it is logged as
    /// <c>Information</c> and its message returned; anything else is a defect and is logged as
    /// <c>Error</c> with the full exception, the caller seeing only <paramref name="fallback"/>.
    /// </summary>
    /// <param name="logger">Logger of the calling handler.</param>
    /// <param name="exception">The caught exception.</param>
    /// <param name="fallback">Author-written text for the non-opted-in case.</param>
    /// <param name="operation">
    /// What was being attempted, with the identifiers worth correlating on
    /// (e.g. <c>$"adding locale '{culture}' to data list {id}"</c>).
    /// </param>
    public static string LogAndResolve(
        ILogger logger,
        Exception? exception,
        string fallback,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var message = MessageOr(exception, fallback);
        if (exception is IEndUserSafeError)
        {
            logger.LogInformation("Rejected {Operation}: {Reason}", operation, message);
        }
        else
        {
            logger.LogError(exception, "Unexpected failure while {Operation}", operation);
        }

        return message;
    }
}
