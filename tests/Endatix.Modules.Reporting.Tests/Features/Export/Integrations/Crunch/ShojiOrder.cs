using System.Text.Json;

namespace Endatix.Modules.Reporting.Tests.Features.Export.Integrations.Crunch;

/// <summary>Reads a Shoji <c>order</c>: aliases and <c>{"group", "entities"}</c> folders, nested.</summary>
internal static class ShojiOrder
{
    /// <summary>Every alias in the order, folders walked depth first, duplicates kept.</summary>
    public static List<string> Aliases(JsonElement order) =>
        order.EnumerateArray()
            .SelectMany(entry => entry.ValueKind == JsonValueKind.String
                ? [entry.GetString()!]
                : Aliases(entry.GetProperty("entities")))
            .ToList();

    /// <summary>The names of the folders directly in this order, in order.</summary>
    public static List<string> FolderNames(JsonElement order) =>
        order.EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Object)
            .Select(entry => entry.GetProperty("group").GetString()!)
            .ToList();

    /// <summary>The entities of the folder at this path of folder names.</summary>
    public static JsonElement Folder(JsonElement order, params string[] path) =>
        path.Aggregate(order, (entities, name) => entities.EnumerateArray()
            .Single(entry => entry.ValueKind == JsonValueKind.Object && entry.GetProperty("group").GetString() == name)
            .GetProperty("entities"));

    /// <summary>The aliases directly in this order, folders left out.</summary>
    public static List<string> TopLevelAliases(JsonElement order) =>
        order.EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetString()!)
            .ToList();
}
