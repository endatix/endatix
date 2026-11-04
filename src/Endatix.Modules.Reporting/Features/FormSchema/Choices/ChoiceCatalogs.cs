using System.Text.Json;
using Storage = Endatix.Modules.Reporting.Features.FormSchema.Choices.OtherStorage;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>One choice catalog entry as persisted: value, positional id, kind and exclusivity.</summary>
internal readonly record struct CatalogChoice(string Value, int Id, ChoiceKind Kind, bool Exclusive)
{
    /// <summary>Null for an entry without a string value or a numeric id.</summary>
    public static CatalogChoice? Read(JsonElement entry)
    {
        var value = entry.GetStringProperty(SurveyJsPropertyNames.Value);
        if (value is null ||
            !entry.TryGetProperty(FormSchemaCodebookPropertyNames.Id, out var id) ||
            id.ValueKind != JsonValueKind.Number ||
            !id.TryGetInt32(out var idValue))
        {
            return null;
        }

        return new CatalogChoice(
            value,
            idValue,
            SpecialChoices.ParseJsonName(entry.GetStringProperty(FormSchemaCodebookPropertyNames.Kind)),
            entry.GetBooleanProperty(FormSchemaCodebookPropertyNames.Exclusive));
    }

    /// <summary>Refused and Don't know: non-substantive answers, which Crunch keeps out of the valid base.</summary>
    public static bool IsMissingCategory(JsonElement entry) => Read(entry)?.Kind.IsNonResponse() ?? false;
}

/// <summary>
/// RUN TIME read model of the codebook's <c>choiceCatalogs</c>: the merged union of every compiled definition. Built
/// once per flatten. The only reader of the catalog fields <c>kind</c>, <c>exclusive</c>, <c>multiple</c> and
/// <c>otherStorage</c>.
/// </summary>
internal sealed class ChoiceCatalogs
{
    private readonly Dictionary<string, SelectChoices> _selects;

    private ChoiceCatalogs(Dictionary<string, SelectChoices> selects)
    {
        _selects = selects;
    }

    public static ChoiceCatalogs Empty { get; } = new(new Dictionary<string, SelectChoices>(StringComparer.Ordinal));

    public static ChoiceCatalogs Read(string? codebookJson)
    {
        if (string.IsNullOrWhiteSpace(codebookJson))
        {
            return Empty;
        }

        using var document = JsonDocument.Parse(codebookJson);
        if (document.RootElement.TryGetPropertyValue(FormSchemaCodebookPropertyNames.ChoiceCatalogs) is not
            { ValueKind: JsonValueKind.Object } catalogs)
        {
            return Empty;
        }

        return new ChoiceCatalogs(catalogs.EnumerateObject()
            .Select(catalog => (catalog.Name, Select: SelectChoices.FromCatalog(catalog.Value)))
            .Where(entry => entry.Select is not null)
            .ToDictionary(entry => entry.Name, entry => entry.Select!, StringComparer.Ordinal));
    }

    /// <summary>True when the catalog marks the question as a base select (<c>multiple</c> present).</summary>
    public bool IsSelect(string question) => _selects.ContainsKey(question);

    /// <summary>
    /// The question's select choices, or <see cref="SelectChoices.Unknown"/> for a question that is not a base select
    /// or a codebook compiled before choice kinds. Unknown still reads <c>{value, comment}</c> objects and "-Comment".
    /// </summary>
    public SelectChoices For(string question) =>
        _selects.TryGetValue(question, out var select) ? select : SelectChoices.Unknown;
}

/// <summary>The stored choices of one select question and where its Other text goes.</summary>
internal sealed class SelectChoices
{
    private readonly HashSet<string> _values = new(StringComparer.Ordinal);

    public static SelectChoices Unknown { get; } = new();

    public IReadOnlyList<CatalogChoice> Choices { get; private init; } = [];

    public OtherStorage? OtherStorage { get; private init; }

    internal static SelectChoices? FromCatalog(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Object ||
            !catalog.TryGetProperty(FormSchemaCodebookPropertyNames.Multiple, out _))
        {
            return null;
        }

        var choices = ReadChoices(catalog);
        SelectChoices select = new()
        {
            Choices = choices,
            OtherStorage = ChoiceSet.ParseOtherStorage(catalog.GetStringProperty(FormSchemaCodebookPropertyNames.OtherStorage)),
        };
        select._values.UnionWith(choices.Select(choice => choice.Value));
        return select;
    }

    private static List<CatalogChoice> ReadChoices(JsonElement catalog) =>
        catalog.TryGetArrayProperty(SurveyJsPropertyNames.Choices, out var entries)
            ? [.. entries.EnumerateArray().Select(CatalogChoice.Read).OfType<CatalogChoice>()]
            : [];

    /// <summary>
    /// Reads one answer from a context: the submission root, or a loop panel item. The only code that knows the shapes
    /// SurveyJS stores: scalar, array, <c>{value, comment}</c>, array of objects, typed Other text in the value,
    /// "{q}-Comment" and the legacy <c>{other: "..."}</c>.
    /// </summary>
    public SelectAnswer Read(JsonElement context, string questionName)
    {
        var raw = context.TryGetPropertyValue(questionName);
        List<SelectAnswer.Token> tokens = [];
        JsonElement? otherText = null;
        foreach (var item in Items(raw))
        {
            if (ReadToken(item) is { } token)
            {
                var (normalized, itemOtherText) = Normalize(item, token);
                tokens.Add(normalized);
                otherText ??= itemOtherText;
            }
        }

        otherText ??= CommentOtherText(context, questionName, tokens) ?? LegacyOtherText(raw);
        var single = raw is { ValueKind: not JsonValueKind.Array } && tokens.Count == 1 ? tokens[0].Element ?? raw : raw;
        return new SelectAnswer(tokens, otherText, single);
    }

    // Value storage: a value no definition offered is typed Other text. A 3.x "other" item carries its text itself.
    private (SelectAnswer.Token Token, JsonElement? OtherText) Normalize(JsonElement item, SelectAnswer.Token token)
    {
        if (IsTypedOtherText(token))
        {
            return (new SelectAnswer.Token(SurveyJsPropertyNames.Other, FromBoolean: false, OtherJson), StringJson(token.Element ?? item));
        }

        var comment = token.Value == SurveyJsPropertyNames.Other && item.ValueKind == JsonValueKind.Object
            ? item.TryGetPropertyValue(SurveyJsPropertyNames.Comment)
            : null;
        return (token, comment);
    }

    // Comment storage (and an older codebook) keeps reading "-Comment" as the flatten always has. Value storage reads
    // it only for a stored "other", which a question flipped from comment storage still holds.
    private JsonElement? CommentOtherText(JsonElement context, string questionName, List<SelectAnswer.Token> tokens) =>
        OtherStorage != Storage.Value || tokens.Exists(token => token.Value == SurveyJsPropertyNames.Other)
            ? context.TryGetPropertyValue(questionName + SurveyJsPropertyNames.CommentSuffix)
            : null;

    private static JsonElement? LegacyOtherText(JsonElement? raw) =>
        raw is { ValueKind: JsonValueKind.Object } legacy ? legacy.TryGetPropertyValue(SurveyJsPropertyNames.Other) : null;

    private static readonly JsonElement OtherJson = JsonDocument.Parse("\"other\"").RootElement.Clone();

    private bool IsTypedOtherText(SelectAnswer.Token token) =>
        OtherStorage == Storage.Value &&
        !token.FromBoolean &&
        !_values.Contains(token.Value);

    private static IEnumerable<JsonElement> Items(JsonElement? raw) =>
        raw switch
        {
            { ValueKind: JsonValueKind.Array } array => array.EnumerateArray(),
            { } scalar => [scalar],
            null => [],
        };

    private static SelectAnswer.Token? ReadToken(JsonElement item)
    {
        if (item.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new SelectAnswer.Token(item.ValueKind == JsonValueKind.True ? "true" : "false", FromBoolean: true, item);
        }

        if (item.ValueKind == JsonValueKind.Object)
        {
            return item.TryGetPropertyValue(SurveyJsPropertyNames.Value) is { } value &&
                   value.GetScalarStringValue() is { } objectValue
                ? new SelectAnswer.Token(objectValue, FromBoolean: false, value)
                : null;
        }

        return item.GetScalarStringValue() is { } scalar
            ? new SelectAnswer.Token(scalar, FromBoolean: false, item)
            : null;
    }

    private static JsonElement StringJson(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? element
            : JsonDocument.Parse(JsonSerializer.Serialize(element.GetRawText())).RootElement.Clone();
}

/// <summary>A normalized answer: the stored values ("other" for typed text), plus the Other text if any.</summary>
internal sealed class SelectAnswer
{
    private readonly IReadOnlyList<Token> _tokens;
    private readonly JsonElement? _single;

    internal SelectAnswer(IReadOnlyList<Token> tokens, JsonElement? otherText, JsonElement? single)
    {
        _tokens = tokens;
        _single = single;
        OtherText = otherText;
        Values = tokens.Select(token => token.Value).ToList();
    }

    public static SelectAnswer Empty { get; } = new([], null, null);

    public IReadOnlyList<string> Values { get; }

    public JsonElement? OtherText { get; }

    /// <summary>Whether the answer holds the choice: ordinal on the trimmed value, ignoring case for a boolean answer.</summary>
    public bool Has(string choiceValue)
    {
        var normalized = choiceValue.Trim();
        return _tokens.Any(token => string.Equals(
            token.Value,
            normalized,
            token.FromBoolean ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }

    /// <summary>
    /// A single-select cell: the stored value as SurveyJS wrote it, the value of a <c>{value, comment}</c> object, or
    /// "other" for typed Other text. An array (a multi-select without inline choices) stays as it is.
    /// </summary>
    public JsonElement? SingleValueJson() => _single;

    internal readonly record struct Token(string Value, bool FromBoolean, JsonElement? Element);
}
