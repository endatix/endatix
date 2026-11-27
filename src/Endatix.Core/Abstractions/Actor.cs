using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Abstractions;

/// <summary>The authenticated human or system process responsible for a change.</summary>
[Experimental("ENDATIX0007")]
public sealed record Actor(string Kind, long? UserId)
{
    /// <summary>A change initiated by the application.</summary>
    public static Actor System { get; } = new("system", null);
}

/// <summary>Resolves the acting human from the authenticated request, including assumed sessions.</summary>
[Experimental("ENDATIX0007")]
public interface IActorAccessor
{
    /// <summary>The principal responsible for writes in the current scope.</summary>
    Actor Current { get; }
}
