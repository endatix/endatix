using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Audience.Domain;

/// <summary>
/// One field on one form's audience. <see cref="VariableName"/> is set at create and never changes.
/// </summary>
public sealed class Property : BaseEntity, IAggregateRoot, ITenantOwned
{
    /// <summary>
    /// Database names of the unique indexes on <see cref="Property"/>.
    /// </summary>
    public static class UniqueConstraints
    {
        public const string VariableNamePerForm = "IX_Properties_VariableName";
    }

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
        Validate(args);
        TenantId = args.TenantId;
        FormId = args.FormId;
        Name = args.Name.Trim();
        VariableName = SlugOf(Name);
        DataType = args.DataType;
        SortOrder = args.SortOrder;
        ChoicesJson = args.ChoicesJson;
        AllowsOther = args.AllowsOther;
    }

    private static void Validate(PropertyCreateArgs args)
    {
        Guard.Against.Null(args);
        Guard.Against.NegativeOrZero(args.TenantId);
        Guard.Against.NegativeOrZero(args.FormId);
        DomainValidationException.ThrowIfError(VariableNameError(args.Name), nameof(args.Name));
        if (!AudienceDataTypeCodes.IsKnown(args.DataType))
        {
            throw new ArgumentException($"Unknown audience data type '{args.DataType}'.", nameof(args));
        }

        DomainValidationException.ThrowIfError(
            ChoicesError(args.DataType, args.ChoicesJson, args.AllowsOther),
            nameof(args));
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

    /// <summary>
    /// Checks one cell value against this property's data type and choices.
    /// </summary>
    public string? ValueError(string value) => PropertyValueRules.ValueError(this, value);

    /// <summary>
    /// Choice types need a JSON array of distinct, non-blank keys. Other types take no choices.
    /// </summary>
    public static string? ChoicesError(string dataType, string? choicesJson, bool allowsOther) =>
        PropertyValueRules.ChoicesError(dataType, choicesJson, allowsOther);

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
