using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Infrastructure.Features.Submissions;

internal sealed class NoOpPersonalizationCatalog : IPersonalizationCatalog
{
    public Task<IReadOnlyList<PersonalizationVariableInfo>> ListForFormAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PersonalizationVariableInfo>>([]);
}
