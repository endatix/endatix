namespace Endatix.Core.Abstractions;

/// <summary>
/// Represents a component that is able to generate unique identifiers
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// Generates a unique identifier. Implementation must be tread safe
    /// </summary>
    /// <returns>A unique identifier</returns>
    long CreateId();
}
