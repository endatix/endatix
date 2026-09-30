using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Personalization.Contracts;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One field on one form's audience. <see cref="VariableName"/> is set at create and never changes.
/// </summary>
public sealed class AudienceProperty : BaseEntity, IAggregateRoot, ITenantOwned
{
    private static readonly Regex NonSlug = new("[^a-z0-9]+", RegexOptions.Compiled);

    private AudienceProperty() { }

    public AudienceProperty(
        long tenantId,
        long formId,
        string name,
        string dataType,
        int sortOrder,
        string? choicesJson = null,
        bool allowsOther = false)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrWhiteSpace(name);
        if (!AudienceDataTypeCodes.IsKnown(dataType))
        {
            throw new ArgumentException($"Unknown audience data type '{dataType}'.", nameof(dataType));
        }

        TenantId = tenantId;
        FormId = formId;
        Name = name.Trim();
        VariableName = Slugify(Name);
        DataType = dataType;
        SortOrder = sortOrder;
        ChoicesJson = choicesJson;
        AllowsOther = allowsOther;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary>Immutable export and piping key.</summary>
    public string VariableName { get; private set; } = null!;

    /// <summary>Editable label.</summary>
    public string Name { get; private set; } = null!;

    public string DataType { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public long? DataListId { get; private set; }

    public string? ChoicesJson { get; private set; }

    public bool AllowsOther { get; private set; }

    public void Rename(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Reorder(int sortOrder) => SortOrder = sortOrder;

    public static string Slugify(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);
        string slug = NonSlug.Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');
        if (string.IsNullOrEmpty(slug))
        {
            throw new ArgumentException("Name does not yield a variable name.", nameof(name));
        }

        return slug;
    }
}
