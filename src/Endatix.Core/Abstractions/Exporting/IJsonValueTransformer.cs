using System.Text.Json;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Transforms a JSON element in the export pipeline. Returns null when no change.
/// Receives row for context (e.g. formId, submissionId).
/// </summary>
/// <typeparam name="T">The row type (e.g. SubmissionExportRow).</typeparam>
public interface IJsonValueTransformer<T> where T : class
{
    /// <summary>
    /// Transforms the JSON element. Returns null when no change.
    /// </summary>
    object? Transform(JsonElement element, T row);
}
