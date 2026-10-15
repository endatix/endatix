namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// A form's audience properties as they are now, for exports that name and order the frozen audience columns.
/// The default knows no properties. The audience module replaces it when personalization is on.
/// </summary>
public interface IPersonalizationCatalog
{
    /// <summary>The form's live properties, one per variable name.</summary>
    Task<IReadOnlyList<PersonalizationVariableInfo>> ListForFormAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken);
}

/// <summary>One audience property as an export shows it: current name and sort order.</summary>
public sealed record PersonalizationVariableInfo(string VariableName, string Name, int SortOrder);
