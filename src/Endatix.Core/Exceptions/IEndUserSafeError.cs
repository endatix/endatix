namespace Endatix.Core.Exceptions;

/// <summary>
/// Opt-in marker declaring that an exception's message was authored for the API caller and is safe to
/// return verbatim.
/// </summary>
/// <remarks>
/// Exception text never reaches an HTTP response by default: <see cref="Exception.Message"/> on a BCL or
/// provider exception routinely carries connection strings, SQL and file paths, and a source-guard test
/// fails the build on any <c>ex.Message</c> flowing into a <c>Result</c> factory. This interface is the
/// one sanctioned exception, and it is explicit so intent is distinguishable from accident.
/// <para>
/// Implementing it claims <see cref="EndUserMessage"/> is a constant, or a format over values the caller
/// supplied, with no server-side detail. Never implement it on a type wrapping a provider exception, and
/// never return an inner exception's message. Read it only through <see cref="SafeError"/>.
/// </para>
/// </remarks>
public interface IEndUserSafeError
{
    /// <summary>
    /// Author-written text that is safe to return to the API caller verbatim.
    /// </summary>
    string EndUserMessage { get; }
}
