namespace Endatix.Core.Abstractions;

/// <summary>
/// An aggregate with a Version guarded as a concurrency token: a save made from an older version changes no row.
/// </summary>
public interface IHasVersion
{
    /// <summary>The current version. The aggregate raises it by one in each write it makes, before the save; its first
    /// value is the implementer's to document (some start at 0 until the first write, some at 1).</summary>
    long Version { get; }
}
