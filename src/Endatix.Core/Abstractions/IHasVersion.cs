namespace Endatix.Core.Abstractions;

/// <summary>
/// An aggregate whose every persisted write bumps Version at save, guarded as a concurrency token.
/// </summary>
public interface IHasVersion
{
    /// <summary>The current version; starts at 1 and increases by one per persisted write.</summary>
    long Version { get; }
}
