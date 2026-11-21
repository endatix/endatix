using System.Diagnostics.CodeAnalysis;
namespace Endatix.Core.Exceptions;

/// <summary>
/// A save of a tenant's branding lost a race: another write changed or created the row after it was read. The save
/// rolled back; the caller answers with a conflict, and the client reads the branding again.
/// </summary>
[Experimental("ENDATIX0002")]
public sealed class TenantBrandingConflictException : Exception
{
    /// <summary>Creates the exception with the reason and the provider's failure.</summary>
    public TenantBrandingConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
