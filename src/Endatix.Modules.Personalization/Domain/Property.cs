using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Personalization.Contracts;

namespace Endatix.Modules.Personalization.Domain;

/// <summary>
/// One field on one form's audience. <see cref="VariableName"/> is set at create and never changes.
/// </summary>
public sealed class Property : BaseEntity, IAggregateRoot, ITenantOwned
{
    private static readonly Regex NonSlug = new(
        "[^a-z0-9]+",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private Property()
    {
        VariableName = string.Empty;
        Name = string.Empty;
        DataType = string.Empty;
    }

    public Property(PropertyCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        DomainValidationException.ThrowIfError(VariableNameError(args.Name), nameof(args.Name));
        if (!AudienceDataTypeCodes.IsKnown(args.DataType))
        {
            throw new ArgumentException($"Unknown audience data type '{args.DataType}'.", nameof(args));
        }

        TenantId = args.TenantId;
        FormId = args.FormId;
        Name = args.Name.Trim();
        VariableName = SlugOf(Name);
        DataType = args.DataType;
        SortOrder = args.SortOrder;
        ChoicesJson = args.ChoicesJson;
        AllowsOther = args.AllowsOther;
    }

    public long TenantId { get; private set; }

    public long FormId { get; private set; }

    /// <summary>Immutable export and piping key.</summary>
    public string VariableName { get; private set; }

    /// <summary>Editable label.</summary>
    public string Name { get; private set; }

    public string DataType { get; private set; }

    public int SortOrder { get; private set; }

    public long? DataListId { get; private set; }

    public string? ChoicesJson { get; private set; }

    public bool AllowsOther { get; private set; }

    public void Rename(string name)
    {
        DomainValidationException.ThrowIfError(NameError(name), nameof(name));
        Name = name.Trim();
    }

    public void Reorder(int sortOrder) => SortOrder = sortOrder;

    public static string? NameError(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        return name.Trim().Length > DataSchemaConstants.MAX_NAME_LENGTH
            ? $"Name must be at most {DataSchemaConstants.MAX_NAME_LENGTH} characters."
            : null;
    }

    /// <summary>
    /// Rejects a name that is blank or has no ASCII letter or digit, since the variable name is
    /// built from those characters only.
    /// </summary>
    public static string? VariableNameError(string? name)
    {
        string? nameError = NameError(name);
        if (nameError is not null || name is null)
        {
            return nameError;
        }

        return SlugOf(name).Length == 0
            ? "Name must contain a letter or a digit (a-z, 0-9)."
            : null;
    }

    public static string Slugify(string name)
    {
        DomainValidationException.ThrowIfError(VariableNameError(name), nameof(name));
        return SlugOf(name);
    }

    private static string SlugOf(string name) =>
        NonSlug.Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');
}
