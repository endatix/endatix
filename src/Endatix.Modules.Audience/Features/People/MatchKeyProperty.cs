using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Settings;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// The match-key Property: the form's <c>text</c> Property whose variable name is the tenant's match key (<c>email</c>
/// or <c>external_id</c>). Its value is the participant's identifier wherever values are read (the people list, the
/// participant panel, Preview as, the Snapshot, Coverage): derived, never stored, and it wins over a stored cell. A
/// participant created by identifier alone thus fills <c>{email}</c> without anyone typing it twice (endatix#1365).
/// A Property of another data type is an ordinary one, so an identifier is never typed as a number, date or choice.
/// </summary>
internal static class MatchKeyProperty
{
    /// <summary>
    /// At most one Property (variable names are unique on a form). The match key is read in the same statement, by
    /// <see cref="IdentifierKindReader.Stored"/>.
    /// </summary>
    public static IQueryable<Property> Of(IAudienceDbContext db, long tenantId, long formId)
    {
        IQueryable<string?> storedKind = IdentifierKindReader.Stored(db, tenantId);
        return db.Properties.AsNoTracking().Where(property =>
            property.TenantId == tenantId
            && property.FormId == formId
            && property.DataType == AudienceDataTypeCodes.Text
            && property.VariableName == (storedKind.FirstOrDefault() ?? IdentifierKindReader.Default));
    }

    /// <summary>The match-key Property among Properties already read, for the match key already read.</summary>
    public static MatchKey? In(IEnumerable<Property> properties, string identifierKind) =>
        properties
            .Where(property => property.DataType == AudienceDataTypeCodes.Text && property.VariableName == identifierKind)
            .Select(property => new MatchKey(property.Id, property.Name))
            .FirstOrDefault();

    /// <summary>The form's match-key Property, or <see langword="null"/> when it has none. One statement.</summary>
    public static Task<MatchKey?> FindAsync(IAudienceDbContext db, MatchKeyForm form, CancellationToken cancellationToken) =>
        Of(db, form.TenantId, form.FormId)
            .Select(property => new MatchKey(property.Id, property.Name))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Coverage counts the match-key Property as filled for every participant: it holds each one's identifier, whatever
    /// cells are stored for it.
    /// </summary>
    public static IReadOnlyDictionary<(long PropertyId, bool IsTest), int> Filled(
        IReadOnlyDictionary<(long PropertyId, bool IsTest), int> filled,
        MatchKey? matchKey,
        (int Test, int Real) participants) =>
        matchKey is null
            ? filled
            : new Dictionary<(long PropertyId, bool IsTest), int>(filled)
            {
                [(matchKey.PropertyId, true)] = participants.Test,
                [(matchKey.PropertyId, false)] = participants.Real,
            };

    /// <summary>
    /// Why <paramref name="values"/> cannot be written for the participant whose identifier is
    /// <paramref name="identifier"/>: a value for the match-key Property that is neither empty nor the identifier, since
    /// every read shows the identifier there. The one rule of the panel, the designer's presets and the CSV import.
    /// </summary>
    public static string? WriteError(IReadOnlyDictionary<long, string> values, MatchKey? matchKey, string identifier) =>
        matchKey is not null
        && values.TryGetValue(matchKey.PropertyId, out string? value)
        && value.Length > 0
        && !string.Equals(value, identifier, StringComparison.Ordinal)
            ? $"{matchKey.Name} is the participant identifier and cannot be changed."
            : null;

    /// <summary>
    /// <paramref name="values"/> without the match-key Property's, which is never stored: checked by
    /// <see cref="WriteError"/> first, it is empty or the identifier.
    /// </summary>
    public static IReadOnlyDictionary<long, string> Stored(IReadOnlyDictionary<long, string> values, MatchKey? matchKey) =>
        matchKey is null || !values.ContainsKey(matchKey.PropertyId)
            ? values
            : values.Where(pair => pair.Key != matchKey.PropertyId).ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <summary>
    /// <paramref name="values"/> with the match-key Property holding <paramref name="identifier"/>, as every read shows it.
    /// </summary>
    public static Dictionary<long, string> Shown(
        IReadOnlyDictionary<long, string> values,
        MatchKey? matchKey,
        string identifier)
    {
        Dictionary<long, string> shown = new(values);
        if (matchKey is not null)
        {
            shown[matchKey.PropertyId] = identifier;
        }

        return shown;
    }
}

/// <summary>A form's match-key Property: its id, and its name for a refusal.</summary>
internal sealed record MatchKey(long PropertyId, string Name);

/// <summary>One form of one tenant, whose match-key Property to find.</summary>
internal readonly record struct MatchKeyForm(long TenantId, long FormId);
