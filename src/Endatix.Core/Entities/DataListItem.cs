using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Common.Translations;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a data list item entity.
/// </summary>
public class DataListItem : BaseEntity
{
    /// <summary>
    /// SurveyJS fallback translation key stored in <see cref="Labels"/>.
    /// Prefer <see cref="SurveyJsTranslationKeys.DefaultKey"/>.
    /// </summary>
    public const string DefaultLabelKey = SurveyJsTranslationKeys.DefaultKey;

    /// <summary>
    /// Maximum length of a single culture label value.
    /// </summary>
    public const int MAX_LABEL_LENGTH = 100;

    private static readonly JsonSerializerOptions _jsonOptions = new();

    private string _labelsJson = "{}";
    private Dictionary<string, string>? _labelsCache;
    private string? _labelsCacheJson;

    /// For EF Core.
    private DataListItem() { }

    /// <summary>
    /// Creates a new data list item.
    /// </summary>
    /// <param name="labels">Localized labels. Must include a non-empty <c>default</c> key.</param>
    /// <param name="value">The invariant value.</param>
    public DataListItem(IReadOnlyDictionary<string, string> labels, string value)
    {
        Guard.Against.Null(labels);
        Guard.Against.NullOrWhiteSpace(value);

        SetLabels(labels);
        Value = value.Trim();
    }

    /// <summary>
    /// Creates a monolingual item with only the <c>default</c> label.
    /// </summary>
    public DataListItem(string defaultLabel, string value)
        : this(new Dictionary<string, string>(StringComparer.Ordinal) { [DefaultLabelKey] = defaultLabel }, value)
    {
    }

    /// <summary>
    /// The data list ID.
    /// </summary>
    public long DataListId { get; private set; }

    /// <summary>
    /// The data list.
    /// </summary>
    public DataList DataList { get; private set; } = null!;

    /// <summary>
    /// Persisted JSON document for localized labels (column name <c>Labels</c>).
    /// Queryable as JSON for provider-specific path filters.
    /// </summary>
    public string LabelsJson
    {
        get => _labelsJson;
        private set => _labelsJson = string.IsNullOrWhiteSpace(value) ? "{}" : value;
    }

    /// <summary>
    /// Localized labels keyed by culture code, always including <see cref="DefaultLabelKey"/>.
    /// Rebuilds when <see cref="LabelsJson"/> changes via setter or backing-field materialization.
    /// </summary>
    [NotMapped]
    public IReadOnlyDictionary<string, string> Labels
    {
        get
        {
            if (_labelsCache is null
                || !string.Equals(_labelsCacheJson, _labelsJson, StringComparison.Ordinal))
            {
                _labelsCache = DeserializeLabels(_labelsJson);
                _labelsCacheJson = _labelsJson;
            }

            return _labelsCache;
        }
    }

    /// <summary>
    /// The invariant value of the data list item.
    /// </summary>
    public string Value { get; private set; } = null!;

    /// <summary>
    /// Resolves the default display label (SurveyJS <c>default</c> key), falling back to <see cref="Value"/>.
    /// </summary>
    [NotMapped]
    public string DefaultLabel =>
        Labels.TryGetValue(DefaultLabelKey, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : Value;

    /// <summary>
    /// Attaches the data list item to a data list.
    /// </summary>
    internal void AttachToDataList(DataList dataList)
    {
        Guard.Against.Null(dataList);

        if (DataList is not null)
        {
            if (ReferenceEquals(DataList, dataList) || (DataListId > 0 && DataListId == dataList.Id))
            {
                return;
            }

            throw new InvalidOperationException("DataListItem is already attached to a different DataList.");
        }

        DataList = dataList;
        DataListId = dataList.Id;
    }

    /// <summary>
    /// Updates the data list item.
    /// </summary>
    public void Update(IReadOnlyDictionary<string, string> labels, string value)
    {
        Guard.Against.Null(labels);
        Guard.Against.NullOrWhiteSpace(value);

        SetLabels(labels);
        Value = value.Trim();
    }

    /// <summary>
    /// Removes a culture key from labels when present. Does not remove <see cref="DefaultLabelKey"/>.
    /// </summary>
    internal void RemoveTranslation(string cultureCode)
    {
        var normalized = TranslationCultureNormalizer.Normalize(cultureCode);
        if (TranslationCultureNormalizer.IsSyntheticDefaultKey(normalized))
        {
            throw new InvalidOperationException("The default translation key cannot be removed.");
        }

        Dictionary<string, string> copy = new(Labels, StringComparer.Ordinal);
        if (!copy.Remove(normalized))
        {
            return;
        }

        SetLabels(copy);
    }

    internal static Dictionary<string, string> NormalizeLabels(IReadOnlyDictionary<string, string> labels)
    {
        if (!labels.TryGetValue(DefaultLabelKey, out var defaultLabel) || string.IsNullOrWhiteSpace(defaultLabel))
        {
            throw new ArgumentException("Labels must include a non-empty 'default' entry.", nameof(labels));
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach (var (key, value) in labels)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmedKey = key.Trim();
            var cultureKey = TranslationCultureNormalizer.IsSyntheticDefaultKey(trimmedKey)
                ? SurveyJsTranslationKeys.DefaultKey
                : TranslationCultureNormalizer.Normalize(trimmedKey);
            normalized[cultureKey] = EnforceLabelLength(value.Trim(), nameof(labels));
        }

        if (!normalized.ContainsKey(DefaultLabelKey))
        {
            normalized[DefaultLabelKey] = EnforceLabelLength(defaultLabel.Trim(), nameof(labels));
        }

        return normalized;
    }

    private static string EnforceLabelLength(string trimmedLabel, string paramName)
    {
        if (trimmedLabel.Length > MAX_LABEL_LENGTH)
        {
            throw new ArgumentException(
                $"Each label value cannot exceed {MAX_LABEL_LENGTH} characters.",
                paramName);
        }

        return trimmedLabel;
    }

    private void SetLabels(IReadOnlyDictionary<string, string> labels)
    {
        var normalized = NormalizeLabels(labels);
        var json = JsonSerializer.Serialize(normalized, _jsonOptions);
        LabelsJson = json;
        _labelsCache = normalized;
        _labelsCacheJson = _labelsJson;
    }

    private static Dictionary<string, string> DeserializeLabels(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions);
        return parsed is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(parsed, StringComparer.Ordinal);
    }
}
