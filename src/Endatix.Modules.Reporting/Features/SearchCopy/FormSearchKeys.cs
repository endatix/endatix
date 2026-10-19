using System.Text.Json;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// The keys of a form's search copy (<see cref="FormSchemaEntity.SearchKeys"/>): at most <see cref="MaxKeys"/>, of
/// which the compile fills up to <see cref="MaxAutomatic"/> and people the rest.
/// </summary>
/// <remarks>
/// The list is append-only, like the flattening map, so rows written from it stay valid: a compile adds the
/// searchable columns it does not hold yet, in schema order, while the automatic slots last. Keeping the other slots
/// free means a question added to a large form never takes a slot someone would choose. A key leaves the list only
/// with its column, when a replace compile drops that column from the schema.
/// </remarks>
internal sealed class FormSearchKeys
{
    public const int MaxAutomatic = 50;
    public const int MaxKeys = 64;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private FormSearchKeys(IReadOnlyList<SearchKey> keys) => Keys = keys;

    public static FormSearchKeys Empty { get; } = new([]);

    public IReadOnlyList<SearchKey> Keys { get; }

    public static FormSearchKeys FromJson(string? searchKeysJson) =>
        string.IsNullOrWhiteSpace(searchKeysJson)
            ? Empty
            : new FormSearchKeys(JsonSerializer.Deserialize<List<SearchKey>>(searchKeysJson, JsonOptions) ?? []);

    public string ToJson() => JsonSerializer.Serialize(Keys, JsonOptions);

    /// <summary>
    /// The keys after a compile: those whose column is gone are dropped, a key whose filter changed takes the new one,
    /// and searchable columns are appended while the automatic slots last.
    /// </summary>
    public SearchKeysRefresh Refresh(SearchKeysCompile compile, DateTime now)
    {
        var kept = Keys.Where(key => compile.ColumnKeys.Contains(key.Key)).ToList();
        var current = kept.Select(key => WithCurrentFilterType(key, compile)).ToList();
        var added = Appendable(current, compile, now);

        FormSearchKeys refreshed = new([.. current, .. added]);
        var rowsHoldStaleValues = !current.SequenceEqual(kept) ||
                                  added.Exists(key => compile.PreviousColumnKeys.Contains(key.Key));
        return new SearchKeysRefresh(refreshed, rowsHoldStaleValues);
    }

    private static SearchKey WithCurrentFilterType(SearchKey key, SearchKeysCompile compile) =>
        compile.Searchable.FirstOrDefault(column => column.Key == key.Key) is { } column && column.FilterType != key.FilterType
            ? key with { FilterType = column.FilterType }
            : key;

    private static List<SearchKey> Appendable(List<SearchKey> kept, SearchKeysCompile compile, DateTime now)
    {
        var automaticSlots = MaxAutomatic - kept.Count(key => key.Source == SearchKeySources.Auto);
        var freeSlots = Math.Min(automaticSlots, MaxKeys - kept.Count);
        var held = kept.Select(key => key.Key).ToHashSet(StringComparer.Ordinal);

        return compile.Searchable
            .Where(column => !held.Contains(column.Key))
            .Take(Math.Max(freeSlots, 0))
            .Select(column => new SearchKey(column.Key, SearchKeySources.Auto, column.FilterType, now))
            .ToList();
    }
}

/// <summary>What a compile tells the search keys: the columns it can search, and the schema's keys after and before it.</summary>
internal sealed record SearchKeysCompile(
    IReadOnlyList<SearchableColumn> Searchable,
    IReadOnlySet<string> ColumnKeys,
    IReadOnlySet<string> PreviousColumnKeys);

/// <summary>
/// The keys after a compile, and whether existing rows need their search copy rebuilt: a key was added for a column
/// the rows already hold, or a key's filter changed how its value is normalised.
/// </summary>
internal sealed record SearchKeysRefresh(FormSearchKeys Keys, bool RowsNeedRebuild);
