namespace Endatix.Core.Abstractions;

/// <summary>
/// Marks an aggregate that maintains a monotonic revision — bumped on every business mutation.
/// Carried in integration event payloads so an order-sensitive consumer (e.g. an audit log) can
/// reconstruct order or detect gaps. Distinct from form-definition (content) versioning.
/// </summary>
public interface IHasRevision
{
    /// <summary>The current revision; starts at 1 and increases by one per business mutation.</summary>
    /// <remarks>Read-only here: each aggregate raises its own revision with the events it raises.</remarks>
    long Revision { get; }
}
