using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Endatix.Core.Features.FormDefinitions.Analysis;

/// <summary>
/// Reads what a form definition refers to, in one forward pass over its JSON with no document built. A data list is
/// bound to a question, a matrix column or any other element by the <c>edxDataListId</c> property, as a string or a
/// number, at any depth.
/// </summary>
public static class FormDefinitionReferences
{
    /// <summary>The definition property that binds an element to a data list.</summary>
    public const string DATA_LIST_ID_PROPERTY = "edxDataListId";

    private const int MAX_DEPTH = 256;

    private static readonly byte[] _dataListIdProperty = Encoding.UTF8.GetBytes(DATA_LIST_ID_PROPERTY);

    /// <summary>
    /// The references of one definition. <see cref="FormDefinitionReferenceSet.Empty"/> for a missing or malformed
    /// definition (an invalid escape included), or one nested deeper than the reader allows.
    /// </summary>
    public static FormDefinitionReferenceSet Analyze(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return FormDefinitionReferenceSet.Empty;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(definitionJson));
        try
        {
            var length = Encoding.UTF8.GetBytes(definitionJson, buffer);
            var dataListIds = ReadDataListIds(buffer.AsSpan(0, length));
            return dataListIds.Count == 0 ? FormDefinitionReferenceSet.Empty : new FormDefinitionReferenceSet(dataListIds);
        }
        catch (JsonException)
        {
            return FormDefinitionReferenceSet.Empty;
        }
        catch (InvalidOperationException)
        {
            // An escaped id that is not valid UTF-16 (a lone surrogate): treated as malformed, so nothing is reached.
            return FormDefinitionReferenceSet.Empty;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static HashSet<long> ReadDataListIds(ReadOnlySpan<byte> json)
    {
        HashSet<long> ids = [];
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = MAX_DEPTH });
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName
                && reader.ValueTextEquals(_dataListIdProperty)
                && reader.Read()
                && TryReadId(ref reader, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static bool TryReadId(ref Utf8JsonReader reader, out long id)
    {
        id = 0;
        var parsed = reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out id),
            JsonTokenType.String => long.TryParse(
                reader.GetString().AsSpan().Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out id),
            _ => false,
        };

        return parsed && id > 0;
    }
}
