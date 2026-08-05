using Ardalis.GuardClauses;
using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents a data list entity.
/// </summary>
public class DataList : TenantEntity, IAggregateRoot, IHasTranslations
{
    public static class UniqueConstraints
    {
        public const string NamePerTenant = "IX_DataLists_TenantId_NormalizedName_Unique";
    }

    private readonly List<DataListItem> _items = [];
    private readonly List<string> _availableLocales = [];

    private DataList() { }

    public DataList(
        long tenantId,
        string name,
        string? description = null,
        string? normalizedName = null,
        string? defaultLocale = null)
        : base(tenantId)
    {
        Guard.Against.NullOrWhiteSpace(name);
        normalizedName ??= name;
        Guard.Against.NullOrWhiteSpace(normalizedName);
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
        DefaultLocale = TranslationCultureNormalizer.Normalize(
            defaultLocale ?? SurveyJsTranslationKeys.FallbackDefaultCulture);
    }

    /// <summary>
    /// The name of the data list.
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// The normalized name of the data list.
    /// </summary>
    public string NormalizedName { get; private set; } = null!;

    /// <summary>
    /// The description of the data list.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether the data list is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Real culture represented by the SurveyJS <c>default</c> label key (e.g. <c>en</c>).
    /// Persistence/API wire name kept as DefaultLocale for stability.
    /// </summary>
    public string DefaultLocale { get; private set; } = SurveyJsTranslationKeys.FallbackDefaultCulture;

    /// <summary>
    /// Added cultures for this list (culture catalog). Does not include the synthetic <c>default</c> key.
    /// Source of truth for validation and list filtering — not derived from item labels.
    /// Mutate only via domain methods; EF Core maps the <c>_availableLocales</c> backing field.
    /// </summary>
    public IReadOnlyList<string> AvailableLocales => _availableLocales.AsReadOnly();

    /// <inheritdoc />
    public string DefaultCulture => DefaultLocale;

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableCultures => _availableLocales.AsReadOnly();

    /// <inheritdoc />
    public int MaxAvailableCultures => IHasTranslations.DEFAULT_MAX_AVAILABLE_CULTURES;

    /// <summary>
    /// The items of the data list.
    /// </summary>
    public IReadOnlyCollection<DataListItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Updates the details of the data list.
    /// </summary>
    public void UpdateDetails(string name, string? description, string normalizedName)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(normalizedName);
        Name = name;
        NormalizedName = normalizedName;
        Description = description;
    }

    /// <inheritdoc />
    public void SetDefaultCulture(string cultureCode)
    {
        var normalized = TranslationCultureNormalizer.Normalize(cultureCode);
        if (TranslationCultureNormalizer.IsSyntheticDefaultKey(normalized))
        {
            throw new ArgumentException(
                "DefaultCulture must be a real culture code (e.g. 'en'), not the synthetic 'default' key.",
                nameof(cultureCode));
        }

        DefaultLocale = normalized;
    }

    /// <inheritdoc />
    public void AddCulture(string cultureCode)
    {
        var normalized = TranslationCultureNormalizer.Normalize(cultureCode);
        if (TranslationCultureNormalizer.IsSyntheticDefaultKey(normalized))
        {
            throw new ArgumentException("The synthetic 'default' key cannot be added as a culture.", nameof(cultureCode));
        }

        if (_availableLocales.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (_availableLocales.Count >= MaxAvailableCultures)
        {
            throw new InvalidOperationException($"A data list cannot have more than {MaxAvailableCultures} cultures.");
        }

        _availableLocales.Add(normalized);
    }

    /// <inheritdoc />
    public void RemoveCulture(string cultureCode)
    {
        var normalized = TranslationCultureNormalizer.Normalize(cultureCode);
        if (TranslationCultureNormalizer.IsSyntheticDefaultKey(normalized))
        {
            throw new ArgumentException("The synthetic 'default' key cannot be removed.", nameof(cultureCode));
        }

        var removed = _availableLocales.RemoveAll(x =>
            string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            item.RemoveTranslation(normalized);
        }
    }

    /// <summary>
    /// Adds a new item to the data list.
    /// </summary>
    public DataListItem AddItem(IReadOnlyDictionary<string, string> labels, string value)
    {
        DataListItem item = CreateItem(labels, value);
        item.AttachToDataList(this);
        _items.Add(item);
        return item;
    }

    /// <summary>
    /// Adds a monolingual item (default label only).
    /// </summary>
    public DataListItem AddItem(string defaultLabel, string value) =>
        AddItem(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SurveyJsTranslationKeys.DefaultKey] = defaultLabel
            },
            value);

    /// <summary>
    /// Replaces the items of the data list.
    /// Validates and materializes all incoming items before mutating the existing collection.
    /// </summary>
    public void ReplaceItems(IEnumerable<(IReadOnlyDictionary<string, string> Labels, string Value)> items)
    {
        Guard.Against.Null(items);

        List<DataListItem> prepared = [];
        foreach (var (labels, value) in items)
        {
            prepared.Add(CreateItem(labels, value));
        }

        _items.Clear();
        foreach (var item in prepared)
        {
            item.AttachToDataList(this);
            _items.Add(item);
        }
    }

    /// <summary>
    /// Sets the active state of the data list.
    /// </summary>
    public void SetActive(bool isActive) => IsActive = isActive;

    /// <inheritdoc />
    public bool AllowsTranslationKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (TranslationCultureNormalizer.IsSyntheticDefaultKey(key))
        {
            return true;
        }

        var normalized = TranslationCultureNormalizer.Normalize(key);
        return _availableLocales.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private DataListItem CreateItem(IReadOnlyDictionary<string, string> labels, string value)
    {
        ValidateLabelKeys(labels);
        return new DataListItem(labels, value);
    }

    private void ValidateLabelKeys(IReadOnlyDictionary<string, string> labels)
    {
        Guard.Against.Null(labels);
        foreach (var key in labels.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!AllowsTranslationKey(key))
            {
                throw new ArgumentException(
                    $"Culture '{key}' is not in the data list culture catalog. Add the culture before assigning labels.",
                    nameof(labels));
            }
        }
    }
}
