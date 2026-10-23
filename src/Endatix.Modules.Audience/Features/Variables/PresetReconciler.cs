using System.Text.Json.Nodes;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// The writes that bring a form's Test participants in line with the designer's edited presets, matched by identifier.
/// Pure, so the rules are tested without a database, and a refusal is found before anything is written.
/// </summary>
internal static class PresetReconciler
{
    /// <summary>
    /// A new identifier becomes a Test participant with its filled values. A Test participant gets the values that
    /// differ from their cells, compared as the variables read types them, a value left out of the preset clearing its
    /// cell; the rest are not rewritten, so a replay writes nothing. A real participant, a written value its Property
    /// refuses, or an identifier given twice refuses the whole request.
    /// </summary>
    public static Result<IReadOnlyList<PresetWrite>> Plan(
        IReadOnlyList<Property> properties,
        IReadOnlyDictionary<string, OnForm> onForm,
        IReadOnlyList<PresetEdit> presets)
    {
        if (Refusal(onForm, presets) is { } refusal)
        {
            return Refused(refusal);
        }

        List<PresetWrite> writes = presets
            .Select(preset => WriteFor(properties, preset, onForm.GetValueOrDefault(preset.Key)))
            .OfType<PresetWrite>()
            .ToList();
        return writes.Select(write => ValueError(properties, write)).FirstOrDefault(error => error is not null) is { } error
            ? Refused(error)
            : Result.Success<IReadOnlyList<PresetWrite>>(writes);
    }

    private static string? Refusal(IReadOnlyDictionary<string, OnForm> onForm, IReadOnlyList<PresetEdit> presets)
    {
        if (presets.GroupBy(preset => preset.Key).FirstOrDefault(group => group.Count() > 1) is { } twice)
        {
            return $"Each test participant can appear once: '{twice.Skip(1).First().Name}'.";
        }

        return presets.Any(preset => onForm.GetValueOrDefault(preset.Key) is { IsTest: false })
            ? "Real participants are read-only."
            : null;
    }

    private static Result<IReadOnlyList<PresetWrite>> Refused(string message) =>
        Result.Invalid(new ValidationError(message));

    /// <summary>Null when the participant is on the form with these values already.</summary>
    private static PresetWrite? WriteFor(IReadOnlyList<Property> properties, PresetEdit preset, OnForm? existing)
    {
        Dictionary<long, string> values = ValuesOf(properties, preset.Variables);
        if (existing is null)
        {
            return new PresetWrite(preset.Name, null, PropertyValuesWriter.Stored(values));
        }

        Dictionary<long, string> changed = properties
            .Where(property => values[property.Id] != AsRead(property, existing.Cells))
            .ToDictionary(property => property.Id, property => values[property.Id]);
        return changed.Count == 0 ? null : new PresetWrite(preset.Name, existing.MembershipId, changed);
    }

    /// <summary>
    /// Checked only for the values that will be written, so a value stored before a rule changed is kept.
    /// </summary>
    private static string? ValueError(IReadOnlyList<Property> properties, PresetWrite write) =>
        properties
            .Where(property => write.Values.ContainsKey(property.Id))
            .Select(property => PropertyValuesWriter.ValueError(property, write.Values[property.Id]))
            .FirstOrDefault(error => error is not null) is { } error
            ? $"{write.Identifier}: {error}"
            : null;

    /// <summary>Every Property's cell text; a variable that is no Property is ignored.</summary>
    private static Dictionary<long, string> ValuesOf(IReadOnlyList<Property> properties, JsonObject variables) =>
        properties.ToDictionary(
            property => property.Id,
            property => AudienceValueJson.Untyped(property.DataType, variables[property.VariableName]));

    /// <summary>A stored cell as the designer sends it back: typed as the variables read types it, then untyped.</summary>
    private static string AsRead(Property property, IReadOnlyDictionary<long, string> cells) =>
        AudienceValueJson.Untyped(
            property.DataType,
            AudienceValueJson.Typed(property.DataType, cells.GetValueOrDefault(property.Id, "")));
}

/// <summary>
/// One edited preset: <see cref="Name"/> is the identifier as the author wrote it, <see cref="Key"/> the same
/// identifier normalized by the tenant's match key, which is how it matches a participant.
/// </summary>
internal sealed record PresetEdit(string Name, string Key, JsonObject Variables);

/// <summary>A participant already on the form, with their live cells by property id.</summary>
internal sealed record OnForm(long MembershipId, bool IsTest, IReadOnlyDictionary<long, string> Cells);

/// <summary>
/// Add <see cref="Identifier"/> as a Test participant when <see cref="MembershipId"/> is null, else set that
/// participant's changed <see cref="Values"/> (an empty value clears the cell).
/// </summary>
internal sealed record PresetWrite(string Identifier, long? MembershipId, IReadOnlyDictionary<long, string> Values);
