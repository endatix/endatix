using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.FormDrafts;

/// <summary>A survey element with its identity-keyed address within the checked document.</summary>
[Experimental("ENDATIX0007")]
public sealed record LocatedSurveyElement(JsonElement Node, FindingPath Path)
{
    /// <summary>Locate nested elements using the same sibling selectors as draft comparisons.</summary>
    public static IEnumerable<LocatedSurveyElement> In(JsonElement definition) => Walk(definition, "", null);

    private static IEnumerable<LocatedSurveyElement> Walk(JsonElement container, string path, string? page)
    {
        foreach (var property in new[] { "pages", "elements", "templateElements" })
        {
            if (container.ValueKind != JsonValueKind.Object || !container.TryGetProperty(property, out var array)
                || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var item in Members(array, property, new Location(path, page)))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<LocatedSurveyElement> Members(JsonElement array, string property, Location location)
    {
        var parent = SurveyDiff.Child(location.Path, property);
        var selectors = SurveyArrayIdentity.Index(array, property)?.Keys.ToArray();
        var index = 0;
        foreach (var node in array.EnumerateArray())
        {
            var address = selectors is null ? SurveyDiff.IndexPath(parent, index) : SurveyDiff.Child(parent, selectors[index]);
            index++;
            var currentPage = property == "pages" ? address : location.Page;
            if (property != "pages")
            {
                yield return new LocatedSurveyElement(node, new(currentPage, address));
            }
            foreach (var child in Walk(node, address, currentPage))
            {
                yield return child;
            }
        }
    }

    private readonly record struct Location(string Path, string? Page);
}
