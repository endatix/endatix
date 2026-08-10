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

    /// <summary>
    /// Maximum number of items allowed in a single data list.
    /// </summary>
    public const int MAX_ITEMS = 5_000;

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
        DefaultLocale = CultureCode.Parse(
            defaultLocale ?? SurveyJsTranslationKeys.FallbackDefaultCulture).Value;
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
    /// <exception cref="ArgumentException">Thrown when the culture code is the synthetic 'default' key.</exception>
    public void SetDefaultCulture(CultureCode cultureCode)
    {
        if (cultureCode.IsSyntheticDefault)
        {
            throw new ArgumentException(
                "DefaultCulture must be a real culture code (e.g. 'en'), not the synthetic 'default' key.",
                nameof(cultureCode));
        }

        DefaultLocale = cultureCode.Value;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when the culture code is the synthetic 'default' key.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the data list has more than the maximum allowed cultures.</exception>
    public void AddCulture(CultureCode cultureCode)
    {
        if (cultureCode.IsSyntheticDefault)
        {
            throw new ArgumentException("The synthetic 'default' key cannot be added as a culture.", nameof(cultureCode));
        }

        if (_availableLocales.Contains(cultureCode.Value, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (_availableLocales.Count >= MaxAvailableCultures)
        {
            throw new InvalidOperationException($"A data list cannot have more than {MaxAvailableCultures} cultures.");
        }

        _availableLocales.Add(cultureCode.Value);
    }

    /// <inheritdoc />
    public void RemoveCulture(CultureCode cultureCode)
    {
        if (cultureCode.IsSyntheticDefault)
        {
            throw new ArgumentException("The synthetic 'default' key cannot be removed.", nameof(cultureCode));
        }

        var removed = _availableLocales.RemoveAll(x =>
            string.Equals(x, cultureCode.Value, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            return;
        }

        foreach (var item in _items)
        {
            item.RemoveTranslation(cultureCode);
        }
    }

    /// <summary>
    /// Adds a new item to the data list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the list already has <see cref="MAX_ITEMS"/> items.</exception>
    public DataListItem AddItem(IReadOnlyDictionary<string, string> labels, string value)
    {
        EnsureCanAddItems(1);
        var item = CreateItem(labels, value);
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
    /// <exception cref="InvalidOperationException">Thrown when more than <see cref="MAX_ITEMS"/> items are provided.</exception>
    public void ReplaceItems(IEnumerable<(IReadOnlyDictionary<string, string> Labels, string Value)> items)
    {
        Guard.Against.Null(items);

        var source =
            items as IReadOnlyList<(IReadOnlyDictionary<string, string> Labels, string Value)>
            ?? [.. items];

        if (source.Count > MAX_ITEMS)
        {
            throw new InvalidOperationException(
                $"A data list cannot have more than {MAX_ITEMS} items.");
        }

        List<DataListItem> prepared = new(source.Count);
        foreach (var (labels, value) in source)
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

    /// <summary>
    /// Resolves a request locale to the JSON label key used for search.
    /// Maps omitted / <c>default</c> / <see cref="DefaultLocale"/> to the synthetic <c>default</c> key;
    /// catalog locales (e.g. <c>es</c>) map to themselves. Unknown catalog locales fall back to <c>default</c>.
    /// </summary>
    public string ResolveLabelSearchKey(CultureCode? locale)
    {
        if (locale is null)
        {
            return SurveyJsTranslationKeys.DefaultKey;
        }

        var culture = locale.Value;
        if (IsDefaultKey(culture) || !AllowsTranslationKey(culture))
        {
            return SurveyJsTranslationKeys.DefaultKey;
        }

        return culture.Value;
    }

    /// <summary>
    /// Resolves requested locales to the JSON label keys they may read.
    /// Keeps catalog cultures, folds <c>default</c> / <see cref="DefaultLocale"/> into the synthetic
    /// <c>default</c> key, and drops locales outside the catalog.
    /// </summary>
    public IReadOnlyList<string> ResolveTranslationKeys(IEnumerable<CultureCode>? locales)
    {
        if (locales is null)
        {
            return [];
        }

        List<string> keys = [];
        foreach (var culture in locales)
        {
            var resolved = IsDefaultKey(culture) ? CultureCode.SyntheticDefault : culture;
            if (AllowsTranslationKey(resolved) && !keys.Contains(resolved.Value, StringComparer.Ordinal))
            {
                keys.Add(resolved.Value);
            }
        }

        return keys;
    }

    /// <inheritdoc />
    public bool AllowsTranslationKey(CultureCode key)
    {
        if (key.IsSyntheticDefault)
        {
            return true;
        }

        return _availableLocales.Contains(key.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool IsDefaultKey(CultureCode cultureCode) =>
        cultureCode.IsSyntheticDefault
        || string.Equals(cultureCode.Value, DefaultCulture, StringComparison.OrdinalIgnoreCase);

    private void EnsureCanAddItems(int countToAdd)
    {
        if (_items.Count + countToAdd > MAX_ITEMS)
        {
            throw new InvalidOperationException(
                $"A data list cannot have more than {MAX_ITEMS} items.");
        }
    }

    private DataListItem CreateItem(IReadOnlyDictionary<string, string> labels, string value)
    {
        ValidateLabelKeys(labels);
        return new DataListItem(labels, value);
    }

    /// <summary>
    /// Ensures every non-empty label key is allowed by the culture catalog (or is the synthetic default key).
    /// </summary>
    internal void ValidateLabelKeys(IReadOnlyDictionary<string, string> labels)
    {
        Guard.Against.Null(labels);
        foreach (var key in labels.Keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!CultureCode.TryParse(key, out CultureCode culture) || !AllowsTranslationKey(culture))
            {
                throw new ArgumentException(
                    $"Culture '{key}' is not in the data list culture catalog. Add the culture before assigning labels.",
                    key);
            }
        }
    }
}
