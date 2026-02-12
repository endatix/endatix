using System.Text.Json.Nodes;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Transforms a value in the export pipeline. Return the same node (if modified in place) or a new node.
/// Return null if the value should be removed/null.
/// </summary>
public interface IValueTransformer
{
    /// <summary>
    /// Transforms a mutable JsonNode. 
    /// Returns the same node (if modified in place) or a new node.
    /// Returns null if the value should be removed/null.
    /// </summary>
    JsonNode? Transform<T>(JsonNode? node, TransformationContext<T> context);
}
