using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Entities;
using Endatix.Core.Features.FormDefinitions.Analysis;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Reads a form's state by its key once per request (scoped), so a change of visibility, enabled state or deletion
/// holds at once on every node. The data lists a definition binds are analyzed once per definition version and
/// cached under that version: a definition saved on any node has a new version, so no node serves a stale analysis.
/// Entries also carry the form's tags, so a save on this node drops them early.
/// </summary>
internal sealed class FormRuntimeScopeReader(
    IRepository<Form> formRepository,
    IRepository<FormDefinition> definitionRepository,
    HybridCache cache,
    ILogger<FormRuntimeScopeReader> logger) : IFormRuntimeScopeReader
{
    private static readonly TimeSpan _referencesTtl = TimeSpan.FromMinutes(30);
    private static readonly HybridCacheEntryOptions _referencesOptions = new()
    {
        Expiration = _referencesTtl,
        LocalCacheExpiration = _referencesTtl,
    };

    private readonly Dictionary<long, FormProjections.FormRuntimeStateDto?> _states = [];

    /// <inheritdoc />
    public async Task<FormRuntimeScope?> GetAsync(long formId, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(formId, cancellationToken);
        return state is null ? null : new FormRuntimeScope(state.Id, state.TenantId, state.IsPublic, state.IsEnabled);
    }

    /// <inheritdoc />
    public async Task<bool> ReachesDataListAsync(long formId, long dataListId, DataListReach reach, CancellationToken cancellationToken)
    {
        var reaches = await CheckDataListAsync(formId, dataListId, reach, cancellationToken);
        if (!reaches)
        {
            logger.LogDataListReadRefused(formId);
        }

        return reaches;
    }

    private async Task<bool> CheckDataListAsync(long formId, long dataListId, DataListReach reach, CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(formId, cancellationToken);
        if (state is null)
        {
            return false;
        }

        // The active definition comes with the state, so it is tried first: most reads end here with no other query.
        var active = state is { ActiveDefinitionId: { } activeId, ActiveDefinitionVersion: { } version }
            ? new FormDefinitionProjections.FormDefinitionVersionDto(activeId, version)
            : null;
        if (active is not null && (await GetDataListIdsAsync(formId, active, cancellationToken)).Contains(dataListId))
        {
            return true;
        }

        if (reach != DataListReach.AnyDefinition)
        {
            return false;
        }

        var definitions = await definitionRepository.ListAsync(
            new FormDefinitionProjections.VersionsByFormIdSpec(formId),
            cancellationToken);
        var others = definitions.Where(definition => definition.Id != active?.Id);
        foreach (var definition in others)
        {
            var dataListIds = await GetDataListIdsAsync(formId, definition, cancellationToken);
            if (dataListIds.Contains(dataListId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<FormProjections.FormRuntimeStateDto?> GetStateAsync(long formId, CancellationToken cancellationToken)
    {
        if (!_states.TryGetValue(formId, out var state))
        {
            state = await formRepository.SingleOrDefaultAsync(new FormProjections.RuntimeStateSpec(formId), cancellationToken);
            _states[formId] = state;
        }

        return state;
    }

    private async Task<long[]> GetDataListIdsAsync(
        long formId,
        FormDefinitionProjections.FormDefinitionVersionDto definition,
        CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"meta:form_definition:references:{definition.Id}:{definition.Version.Ticks}",
            async ct =>
            {
                var json = await definitionRepository.SingleOrDefaultAsync(
                    new FormDefinitionProjections.JsonByIdSpec(definition.Id),
                    ct);
                return FormDefinitionReferences.Analyze(json).DataListIds.ToArray();
            },
            _referencesOptions,
            FormAccessCacheTags.ForFormAndAccess(formId),
            cancellationToken);
}
