using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export.Tabular;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// DI-backed registry of <see cref="IColumnAliasTransformer"/> strategies.
/// </summary>
internal sealed class ColumnAliasTransformerRegistry : IColumnAliasTransformerRegistry
{
    private readonly IReadOnlyDictionary<ColumnAliasProfile, IColumnAliasTransformer> _transformers;

    /// <summary>
    /// Built-in Native + question-index registry for tests and callers without DI.
    /// </summary>
    internal static ColumnAliasTransformerRegistry Default { get; } = new(
    [
        NativeColumnAliasTransformer._instance,
        QuestionIndexAliasTransformer._instance,
    ]);

    public ColumnAliasTransformerRegistry(IEnumerable<IColumnAliasTransformer> transformers)
    {
        Dictionary<ColumnAliasProfile, IColumnAliasTransformer> map = new();

        foreach (var transformer in transformers)
        {
            map[transformer.Profile] = transformer;
        }

        if (map.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one IColumnAliasTransformer must be registered.");
        }

        _transformers = map;
        RegisteredProfiles = map.Keys.OrderBy(profile => (int)profile).ToList();
    }

    public IReadOnlyList<ColumnAliasProfile> RegisteredProfiles { get; }

    public IReadOnlyList<ColumnAliasNamingConventionDto> GetCatalog() =>
        RegisteredProfiles
            .Select(profile => _transformers[profile])
            .Select(transformer => new ColumnAliasNamingConventionDto(
                transformer.WireKey,
                transformer.Label,
                transformer.Description,
                transformer.Example))
            .ToList();

    public bool TryGet(ColumnAliasProfile profile, out IColumnAliasTransformer transformer) =>
        _transformers.TryGetValue(profile, out transformer!);

    public IColumnAliasTransformer GetRequired(ColumnAliasProfile profile)
    {
        if (TryGet(profile, out var transformer))
        {
            return transformer;
        }

        throw new InvalidOperationException(
            $"No column alias transformer is registered for profile '{profile}'.");
    }
}
