using System.Text.Json;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.FormDefinitions.GetFields;

/// <summary>
/// Handler for the <c>GetFormDefinitionFieldsQuery</c> class.
/// </summary>
public partial class GetFormDefinitionFieldsHandler(
    IRepository<FormDefinition> formDefinitionsRepository,
    IFormsRepository formsRepository,
    ILogger<GetFormDefinitionFieldsHandler> logger
) : IQueryHandler<GetFormDefinitionFieldsQuery, Result<IEnumerable<DefinitionFieldDto>>>
{
    ///  <inheritdoc/>
    public async Task<Result<IEnumerable<DefinitionFieldDto>>> Handle(
        GetFormDefinitionFieldsQuery request,
        CancellationToken cancellationToken)
    {
        var specification = new FormSpecifications.ByIdReadOnly(request.FormId);
        var form = await formsRepository.SingleOrDefaultAsync(specification, cancellationToken);

        if (form is null)
        {
            return Result.NotFound("Form not found.");
        }

        var spec = new FormDefinitionsByFormIdSpec(request.FormId);
        var definitions = await formDefinitionsRepository.ListAsync(spec, cancellationToken);

        if (definitions.Count == 0)
        {
            return Result.Success(Enumerable.Empty<DefinitionFieldDto>());
        }

        var seen = new Dictionary<string, (string Title, string Type)>(StringComparer.OrdinalIgnoreCase);

        // Union of fields across all definitions; newest definition wins (first pass wins for a given name).
        foreach (var definition in definitions.OrderByDescending(d => d.CreatedAt))
        {
            if (string.IsNullOrWhiteSpace(definition.JsonData))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(definition.JsonData);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetObjectProperty(root, "pages", out var pages) ||
                    pages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var page in pages.EnumerateArray())
                {
                    if (page.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!TryGetObjectProperty(page, "elements", out var elements) ||
                        elements.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    ExtractFields(elements, seen);
                }
            }
            catch (JsonException ex)
            {
                LogJsonParsingError(definition.Id, ex);
                continue;
            }
        }

        var fields = seen.Select(kvp => new DefinitionFieldDto(kvp.Key, kvp.Value.Title, kvp.Value.Type));
        return Result.Success(fields);
    }

    /// <summary>
    /// Safe property read: <c>JsonElement.TryGetProperty</c> throws when the receiver is not a JSON object.
    /// </summary>
    private static bool TryGetObjectProperty(JsonElement obj, string propertyName, out JsonElement value)
    {
        value = default;
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return obj.TryGetProperty(propertyName, out value);
    }

    /// <summary>
    /// Reads a string property; <see cref="JsonElement.GetString"/> throws if the value is not a JSON string.
    /// </summary>
    private static bool TryGetStringProperty(JsonElement obj, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryGetObjectProperty(obj, propertyName, out var prop) ||
            prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var str = prop.GetString();
        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        value = str;
        return true;
    }

    private static void ExtractFields(JsonElement elements, Dictionary<string, (string Title, string Type)> seen)
    {
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryGetElementType(element, out var type))
            {
                continue;
            }

            if (TryHandleSurveyContainers(element, type, seen))
            {
                continue;
            }

            if (!TryGetElementName(element, out var name))
            {
                continue;
            }

            if (!seen.ContainsKey(name))
            {
                var title = ResolveElementTitle(element, name);
                seen[name] = (title, type);
            }
        }
    }

    /// <summary>
    /// SurveyJS container types that hold nested field definitions (not leaf questions for this pass).
    /// </summary>
    private static bool TryHandleSurveyContainers(
        JsonElement element,
        string type,
        Dictionary<string, (string Title, string Type)> seen)
    {
        if (type.Equals("panel", StringComparison.OrdinalIgnoreCase))
        {
            TryExtractNestedElementArray(element, "elements", seen);
            return true;
        }

        if (type.Equals("paneldynamic", StringComparison.OrdinalIgnoreCase))
        {
            TryExtractNestedElementArray(element, "templateElements", seen);
            return true;
        }

        if (type.Equals("matrixdropdown", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("matrixdynamic", StringComparison.OrdinalIgnoreCase))
        {
            ExtractMatrixColumns(element, seen);
            return true;
        }

        return false;
    }

    private static void TryExtractNestedElementArray(
        JsonElement element,
        string arrayPropertyName,
        Dictionary<string, (string Title, string Type)> seen)
    {
        if (TryGetObjectProperty(element, arrayPropertyName, out var nested) &&
            nested.ValueKind == JsonValueKind.Array)
        {
            ExtractFields(nested, seen);
        }
    }

    /// <summary>
    /// Matrix columns use <c>name</c> / <c>title</c>; question type is often expressed as <c>cellType</c>.
    /// </summary>
    private static void ExtractMatrixColumns(JsonElement matrixQuestion, Dictionary<string, (string Title, string Type)> seen)
    {
        if (!TryGetObjectProperty(matrixQuestion, "columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var column in columns.EnumerateArray())
        {
            if (column.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryGetStringProperty(column, "name", out var nameValue))
            {
                continue;
            }

            if (seen.ContainsKey(nameValue))
            {
                continue;
            }

            var title = ResolveElementTitle(column, nameValue);
            var cellType = "text";
            if (TryGetStringProperty(column, "cellType", out var ct))
            {
                cellType = ct;
            }

            seen[nameValue] = (title, cellType);
        }
    }

    /// <summary>
    /// Try to get the type of an element.
    /// </summary>
    /// <param name="element">The element to get the type of.</param>
    /// <param name="type">The type of the element.</param>
    /// <returns>True if the type is found, false otherwise.</returns>
    private static bool TryGetElementType(JsonElement element, out string type)
    {
        type = string.Empty;
        return TryGetStringProperty(element, "type", out type);
    }

    /// <summary>
    /// Try to get the name of an element.
    /// </summary>
    /// <param name="element">The element to get the name of.</param>
    /// <param name="name">The name of the element.</param>
    /// <returns>True if the name is found, false otherwise.</returns>
    private static bool TryGetElementName(JsonElement element, out string name)
    {
        name = string.Empty;
        return TryGetStringProperty(element, "name", out name);
    }

    /// <summary>
    /// Resolve the title of an element.
    /// </summary>
    /// <param name="element">The element to resolve the title of.</param>
    /// <param name="fallbackName">The fallback name to use if the title is not found.</param>
    /// <returns>The title of the element.</returns>
    private static string ResolveElementTitle(JsonElement element, string fallbackName)
    {
        if (!TryGetObjectProperty(element, "title", out var titleProp) ||
            titleProp.ValueKind != JsonValueKind.String)
        {
            return fallbackName;
        }

        var title = titleProp.GetString();
        return string.IsNullOrWhiteSpace(title) ? fallbackName : title;
    }

    [LoggerMessage(
        EventId = 51001,
        Level = LogLevel.Error,
        EventName = "FormDefinitionJsonParseFailed",
        Message = "Error parsing JSON for form definition {DefinitionId}")]
    private partial void LogJsonParsingError(long definitionId, JsonException ex);
}
