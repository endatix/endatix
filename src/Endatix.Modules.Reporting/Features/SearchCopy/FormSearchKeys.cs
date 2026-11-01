using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// The keys of a form's search copy (<see cref="FormSchemaEntity.SearchKeys"/>): at most <see cref="MaxKeys"/>, of
/// which the compile fills up to <see cref="MaxAutomatic"/> and people up to <see cref="MaxUserKeys"/>.
/// </summary>
/// <remarks>
/// <para>
/// The compile only appends, like the flattening map, so rows written from the list stay valid: it adds the
/// searchable columns it does not hold yet, in schema order, while the automatic slots last. Keeping the other slots
/// free means a question added to a large form never takes a slot someone would choose. A key leaves the list with
/// its column, when a replace compile drops that column, or when a person removes a key they added.
/// </para>
/// <para>
/// <see cref="Revision"/> tells rows that hold a key's values from rows that do not hold them yet. It moves forward
/// whenever existing rows need a value they were not written with (a key added for a column they hold, or a key
/// whose normalisation changed), and that key takes the new revision. Each row is stamped with the revision of the
/// keys it was written from, so a row older than a key's revision is not checked on that key until it is rebuilt.
/// </para>
/// </remarks>
internal sealed class FormSearchKeys
{
    public const int MaxAutomatic = 50;
    public const int MaxKeys = 64;
    public const int MaxUserKeys = MaxKeys - MaxAutomatic;

    internal static readonly string CapMessage =
        $"A form can have at most {MaxUserKeys} filterable questions chosen by people, and {MaxKeys} in all.";

    internal const string AutomaticKeyMessage = "A question made filterable automatically cannot be removed.";
    internal const string NotSearchableMessage = "The question is not filterable on this form.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private FormSearchKeys(IReadOnlyList<SearchKey> keys, int revision)
    {
        Keys = keys;
        Revision = revision;
    }

    public static FormSearchKeys Empty { get; } = new([], 0);

    public IReadOnlyList<SearchKey> Keys { get; }

    public int Revision { get; }

    public int UserKeyCount => Keys.Count(key => key.Source == SearchKeySources.User);

    public static FormSearchKeys Of(FormSchemaEntity schema) => Parse(schema.SearchKeys, schema.SearchKeysRevision);

    /// <summary>The keys alone, for a reader that needs no form revision; <see cref="Revision"/> is 0.</summary>
    public static FormSearchKeys FromJson(string? searchKeysJson) => Parse(searchKeysJson, 0);

    public string ToJson() => JsonSerializer.Serialize(Keys, JsonOptions);

    public SearchKey? Find(string key) => Keys.FirstOrDefault(searchKey => searchKey.Key == key);

    /// <summary>
    /// The revision a row must be stamped with to hold every one of <paramref name="keys"/>; 0 when none of them is a
    /// search key.
    /// </summary>
    public int RevisionNeededFor(IEnumerable<string> keys)
    {
        var needed = keys.ToHashSet(StringComparer.Ordinal);
        return Keys.Where(key => needed.Contains(key.Key)).Select(key => key.Revision).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// The keys after a compile: those whose column is gone, or that no answer filter supports any more, are dropped; a
    /// key whose filter changed takes the new one, whoever chose it; and columns the compile may choose are appended
    /// while the automatic slots last. The revision moves forward when existing
    /// rows need a value they were not written with: a key added for a column they hold, or a changed filter.
    /// </summary>
    public FormSearchKeys Refresh(SearchKeysCompile compile, DateTime now)
    {
        var filterable = compile.Searchable.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        var kept = Keys.Where(key => filterable.Contains(key.Key)).ToList();
        var current = kept.Select(key => WithCurrentFilterType(key, compile)).ToList();
        var added = Appendable(current, compile, now);
        var rowsHoldStaleValues = !current.SequenceEqual(kept) ||
                                  added.Exists(key => compile.PreviousColumnKeys.Contains(key.Key));

        var revision = rowsHoldStaleValues ? Revision + 1 : Revision;
        var stamped = current.Select((key, index) => key == kept[index] ? key : key with { Revision = revision });
        return new FormSearchKeys([.. stamped, .. added.Select(key => key with { Revision = revision })], revision);
    }

    /// <summary>
    /// The keys with <paramref name="column"/> added by a person, at a new revision; the same keys when it already is
    /// one; invalid when the slots for people, or all slots, are taken.
    /// </summary>
    public Result<FormSearchKeys> AddUser(SearchableColumn column, DateTime now)
    {
        if (Find(column.Key) is not null)
        {
            return Result.Success(this);
        }

        if (UserKeyCount >= MaxUserKeys || Keys.Count >= MaxKeys)
        {
            return Result<FormSearchKeys>.Invalid(new ValidationError("key", CapMessage, "SearchKeysCap", ValidationSeverity.Error));
        }

        var revision = Revision + 1;
        SearchKey added = new(column.Key, SearchKeySources.User, column.FilterType, now, revision);
        return Result.Success(new FormSearchKeys([.. Keys, added], revision));
    }

    /// <summary>
    /// The keys without <paramref name="key"/>, which a person added; filters stop reading it at once. Not found when
    /// it is not a search key, invalid when the compile chose it.
    /// </summary>
    public Result<FormSearchKeys> RemoveUser(string key)
    {
        var removed = Find(key);
        if (removed is null)
        {
            return Result<FormSearchKeys>.NotFound(NotSearchableMessage);
        }

        return removed.Source == SearchKeySources.User
            ? Result.Success(new FormSearchKeys(Keys.Where(searchKey => searchKey != removed).ToList(), Revision))
            : Result<FormSearchKeys>.Invalid(new ValidationError("key", AutomaticKeyMessage, "AutomaticSearchKey", ValidationSeverity.Error));
    }

    private static FormSearchKeys Parse(string? searchKeysJson, int revision) =>
        string.IsNullOrWhiteSpace(searchKeysJson)
            ? new FormSearchKeys([], revision)
            : new FormSearchKeys(JsonSerializer.Deserialize<List<SearchKey>>(searchKeysJson, JsonOptions) ?? [], revision);

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
            .Where(column => column.Automatic && !held.Contains(column.Key))
            .Take(Math.Max(freeSlots, 0))
            .Select(column => new SearchKey(column.Key, SearchKeySources.Auto, column.FilterType, now))
            .ToList();
    }
}

/// <summary>What a compile tells the search keys: the columns an answer filter can read, and the schema's keys before it.</summary>
internal sealed record SearchKeysCompile(IReadOnlyList<SearchableColumn> Searchable, IReadOnlySet<string> PreviousColumnKeys);
