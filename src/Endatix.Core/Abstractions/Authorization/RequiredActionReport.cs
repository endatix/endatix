using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>A provider result whose actions (including no actions) cease to describe standing at a known instant.</summary>
[Experimental("ENDATIX0006")]
public sealed class RequiredActionReport(IEnumerable<RequiredAction> actions, DateTimeOffset validUntil)
    : ReadOnlyCollection<RequiredAction>(actions.ToArray())
{
    /// <summary>The exclusive UTC deadline for both fresh results and optional fallback.</summary>
    public DateTimeOffset ValidUntil { get; } = validUntil.ToUniversalTime();
}
