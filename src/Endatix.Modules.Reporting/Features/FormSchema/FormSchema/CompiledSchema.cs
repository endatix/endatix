using System.Text.Json;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>A compiled schema and the revision it is saved at.</summary>
internal sealed record CompiledSchema(long Revision, FormSchemaCompileResult Result)
{
    /// <summary>
    /// The revision only moves forward, except on a replace: that holds one definition's columns only, so it takes
    /// that definition's revision.
    /// </summary>
    public static long NextRevision(FormSchemaEntity existing, long formDefinitionId, FormSchemaCompileMode mode) =>
        mode == FormSchemaCompileMode.Replace
            ? formDefinitionId
            : Math.Max(existing.FormDefinitionRevision, formDefinitionId);

    /// <summary>
    /// Whether the stored schema already is this one. Stored JSON comes back normalized from <c>jsonb</c>, so it is
    /// compared by value, not as text.
    /// </summary>
    public bool Matches(FormSchemaEntity existing) =>
        existing.FormDefinitionRevision == Revision &&
        JsonEquals(existing.FlatteningMap, Result.FlatteningMapJson) &&
        JsonEquals(existing.Codebook, Result.CodebookJson) &&
        JsonEquals(existing.Locales, Result.LocalesJson);

    /// <summary>Writes this schema onto the stored one, at its revision; the caller saves it.</summary>
    public void ApplyTo(FormSchemaEntity existing)
    {
        existing.RevertRevisionTo(Revision);
        existing.UpdateSchema(Revision, Result.FlatteningMapJson, Result.CodebookJson, Result.LocalesJson);
    }

    private static bool JsonEquals(string stored, string compiled)
    {
        using var storedDocument = JsonDocument.Parse(stored);
        using var compiledDocument = JsonDocument.Parse(compiled);
        return JsonElement.DeepEquals(storedDocument.RootElement, compiledDocument.RootElement);
    }
}
