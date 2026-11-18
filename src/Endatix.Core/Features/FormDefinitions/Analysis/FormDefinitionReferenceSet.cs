namespace Endatix.Core.Features.FormDefinitions.Analysis;

/// <summary>
/// What one form definition refers to outside itself. Each kind of reference is its own collection, so a new
/// kind (for example image ids) is a new member, not a change to the existing ones.
/// </summary>
/// <param name="DataListIds">The data lists the definition binds, each once.</param>
public sealed record FormDefinitionReferenceSet(IReadOnlyCollection<long> DataListIds)
{
    /// <summary>A definition that refers to nothing: missing, empty or malformed.</summary>
    public static FormDefinitionReferenceSet Empty { get; } = new([]);
}
