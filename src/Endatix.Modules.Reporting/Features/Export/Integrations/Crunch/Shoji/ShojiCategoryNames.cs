namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// Crunch needs the category names of a variable to be unique. A merged choice catalog can hold a removed choice and a
/// current one with the same label (a value renamed, its label kept): every such category but the newest is named
/// with its value, as in "Blue (b)".
/// </summary>
internal static class ShojiCategoryNames
{
    /// <param name="categories">The categories in id order: label, then stored value.</param>
    public static IReadOnlyList<string> Unique(IReadOnlyList<(string Name, string Value)> categories)
    {
        var newest = categories
            .Select((category, index) => (category.Name, Index: index))
            .GroupBy(category => category.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(category => category.Index), StringComparer.Ordinal);

        return categories
            .Select((category, index) => newest[category.Name] == index
                ? category.Name
                : $"{category.Name} ({category.Value})")
            .ToList();
    }
}
