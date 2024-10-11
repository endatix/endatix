namespace Endatix.Core.Abstractions;

/// <summary>
/// Represents a component that is able to generate unique identifiers of a specific type
/// </summary>
/// <typeparam name="TKey">The type of the identifier</typeparam>
public interface IIdGenerator<TKey> where TKey: IEquatable<TKey>
{
    /// <summary>
    /// Generates a unique identifier of the specified type. Implementation must be tread safe
    /// </summary>
    /// <returns>A unique identifier of the specified type</returns>
    TKey CreateId();
}

