using System.Collections.Generic;
using Ardalis.GuardClauses;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Classes implementing this interface will support passing dynamic data via Metadata property
/// Designed for not persisted classes like Models and DTOs
/// </summary>
public interface IHasMetadata
{
    /// <summary>
    /// Access the instance implementing the <c>IHasMetadata</c> as IHasMetadata to allow for working with the default methods' implementation available in the IHasMetadata interface
    /// </summary>
    IHasMetadata MetadataOperations { get; }

    /// <summary>
    /// The Metadata dictionary. Implementation must ensure it's initialized
    /// </summary>
    Dictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Adds new value to the Metadata based of key
    /// </summary>
    /// <typeparam name="T">Type of the incoming value</typeparam>
    /// <param name="key">The lookup key</param>
    /// <param name="value">The value</param>
    /// <returns>The added value of type T</returns>
    /// <exception cref="ArgumentNullException">When Metadata is not initialized</exception>
    T AddMetadataValue<T>(string key, T value) where T : notnull
    {
        Guard.Against.Null(Metadata);

        if (Metadata.ContainsKey(key))
        {
            Metadata[key] = value;
        }
        else
        {
            Metadata.Add(key, value);
        }

        return value;
    }

    /// <summary>
    /// Retrieves value from the Metadata dictionary using the TryGet pattern
    /// </summary>
    /// <typeparam name="T">Type of the expected value</typeparam>
    /// <param name="key">The lookup key</param>
    /// <param name="value">The value</param>
    /// <returns>True if the value is retrieved, false if no value is retrieved</returns>
    /// <exception cref="ArgumentNullException">When Metadata is not initialized</exception>
    bool TryGetMetadataValue<T>(string key, out T value)
    {
        Guard.Against.Null(Metadata);

        if (Metadata.TryGetValue(key, out var val) && val is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Retrieves value from the Metadata dictionary
    /// </summary>
    /// <typeparam name="T">Type of the expected value</typeparam>
    /// <param name="key">The lookup key</param>
    /// <returns>The retrieved value</returns>
    /// <exception cref="ArgumentNullException">When Metadata is not initialized</exception>
    /// <exception cref="KeyNotFoundException">If no keys is not found in the Metadata dictionary</exception>
    T GetMetadataValue<T>(string key)
    {
        Guard.Against.Null(Metadata);

        if (Metadata.TryGetValue(key, out var val) && val is T typedValue)
        {
            return typedValue;
        }

        throw new KeyNotFoundException($"Key '{key}' not found in metadata.");
    }

    /// <summary>
    /// Removes a value from the Metadata dictionary
    /// </summary>
    /// <param name="key">The lookup key</param>
    /// <returns>True if value is found and deleted, False if the value is not found</returns>
    /// <exception cref="ArgumentNullException">When Metadata is not initialized</exception>
    bool RemoveMetadataValue(string key)
    {
        return Metadata.Remove(key);
    }
}