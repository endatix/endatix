namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Resolves registered <see cref="IColumnAliasTransformer"/> implementations by profile.
/// </summary>
public interface IColumnAliasTransformerRegistry
{
    /// <summary>
    /// Profiles with a registered transformer.
    /// </summary>
    IReadOnlyList<ColumnAliasProfile> RegisteredProfiles { get; }

    /// <summary>
    /// Catalog entries for admin UI (label, description, example).
    /// </summary>
    IReadOnlyList<ColumnAliasNamingConventionDto> GetCatalog();

    /// <summary>
    /// Tries to resolve the transformer for <paramref name="profile"/>.
    /// </summary>
    bool TryGet(ColumnAliasProfile profile, out IColumnAliasTransformer transformer);

    /// <summary>
    /// Returns the transformer for <paramref name="profile"/>, or throws when unregistered.
    /// </summary>
    IColumnAliasTransformer GetRequired(ColumnAliasProfile profile);
}
