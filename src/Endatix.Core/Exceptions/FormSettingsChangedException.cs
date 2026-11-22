using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Exceptions;

/// <summary>
/// A save that wrote a form's settings lost a race: another write raised their version since they were read, or stored
/// the form's first settings row first. The whole save rolled back, and the save's pending changes are no longer
/// tracked, so a later save cannot replay them; the caller answers a conflict.
/// </summary>
/// <remarks>A form save writes its settings too while the form's webhook settings are kept in both places
/// (endatix/endatix#1784), so a form save can lose this race to another form save or to a settings write.</remarks>
[Experimental("ENDATIX0005")]
public sealed class FormSettingsChangedException : Exception
{
    /// <summary>Reports the lost race, with the provider's error as <paramref name="innerException"/>.</summary>
    public FormSettingsChangedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
