using Endatix.Modules.Audience.Contracts;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>The catalog when the Audience module is off: no properties, so audience columns keep their frozen names.</summary>
internal sealed class NoAudiencePropertyCatalog : IAudiencePropertyCatalog
{
    public Task<IReadOnlyList<AudiencePropertyName>> ListForFormAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AudiencePropertyName>>([]);
}
