namespace Endatix.Modules.Audience.Contracts;

/// <summary>
/// A form's audience properties as the Audience tab lists them now. Exports read it to name and order the audience
/// values frozen on responses. With the Audience module off, a consumer registers a default that knows no properties.
/// </summary>
public interface IAudiencePropertyCatalog
{
    /// <summary>The form's live properties in Audience-tab order, one per variable name.</summary>
    Task<IReadOnlyList<AudiencePropertyName>> ListForFormAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken);
}

/// <summary>One audience property: its variable name, fixed at create, its current name, and its sort order.</summary>
public sealed record AudiencePropertyName(string VariableName, string Name, int SortOrder);
