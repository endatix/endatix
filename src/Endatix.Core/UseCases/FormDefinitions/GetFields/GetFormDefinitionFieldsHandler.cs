using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.GetFields;

public class GetFormDefinitionFieldsHandler(
    IRepository<FormDefinition> formDefinitionsRepository
) : IQueryHandler<GetFormDefinitionFieldsQuery, Result<IEnumerable<DefinitionFieldDto>>>
{
    public async Task<Result<IEnumerable<DefinitionFieldDto>>> Handle(
        GetFormDefinitionFieldsQuery request,
        CancellationToken cancellationToken)
    {
        var spec = new FormDefinitionsByFormIdSpec(request.FormId);
        var definitions = await formDefinitionsRepository.ListAsync(spec, cancellationToken);

        if (definitions.Count == 0)
        {
            return Result.NotFound("No form definitions found for the given form.");
        }

        var seen = new Dictionary<string, (string Title, string Type)>(StringComparer.OrdinalIgnoreCase);

        // Union of fields across all definitions; newest definition wins (first occurrence in reverse-chronological order).
        foreach (var definition in definitions.OrderByDescending(d => d.CreatedAt))
        {
            if (string.IsNullOrWhiteSpace(definition.JsonData))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(definition.JsonData);
            if (!doc.RootElement.TryGetProperty("pages", out var pages))
            {
                continue;
            }

            foreach (var page in pages.EnumerateArray())
            {
                if (!page.TryGetProperty("elements", out var elements))
                {
                    continue;
                }

                ExtractFields(elements, seen);
            }
        }

        var fields = seen.Select(kvp => new DefinitionFieldDto(kvp.Key, kvp.Value.Title, kvp.Value.Type));
        return Result.Success(fields);
    }

    private static void ExtractFields(JsonElement elements, Dictionary<string, (string Title, string Type)> seen)
    {
        foreach (var element in elements.EnumerateArray())
        {
            if (!TryGetElementType(element, out var type))
            {
                continue;
            }

            if (TryHandlePanel(element, type, seen))
            {
                continue;
            }

            if (!TryGetElementName(element, out var name))
            {
                continue;
            }

            AddFieldIfNew(element, name, type, seen);
        }
    }

    private static bool TryGetElementType(JsonElement element, out string type)
    {
        type = string.Empty;

        if (!element.TryGetProperty("type", out var typeProp))
        {
            return false;
        }

        var typeValue = typeProp.GetString();
        if (string.IsNullOrWhiteSpace(typeValue))
        {
            return false;
        }

        type = typeValue;
        return true;
    }

    private static bool TryHandlePanel(JsonElement element, string type, Dictionary<string, (string Title, string Type)> seen)
    {
        if (!type.Equals("panel", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (element.TryGetProperty("elements", out var nested))
        {
            ExtractFields(nested, seen);
        }

        return true;
    }

    private static bool TryGetElementName(JsonElement element, out string name)
    {
        name = string.Empty;

        if (!element.TryGetProperty("name", out var nameProp))
        {
            return false;
        }

        var nameValue = nameProp.GetString();
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            return false;
        }

        name = nameValue;
        return true;
    }

    private static void AddFieldIfNew(JsonElement element, string name, string type, Dictionary<string, (string Title, string Type)> seen)
    {
        if (seen.ContainsKey(name))
        {
            return;
        }

        var title = element.TryGetProperty("title", out var titleProp)
            ? titleProp.GetString() ?? name
            : name;

        seen[name] = (title, type);
    }
}
