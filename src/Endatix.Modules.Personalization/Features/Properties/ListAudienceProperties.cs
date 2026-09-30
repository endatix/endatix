using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.Properties;

/// <summary>
/// Lists audience properties for a form, ordered by sort order.
/// </summary>
public sealed record ListAudiencePropertiesQuery(long TenantId, long FormId)
    : IQuery<Result<IReadOnlyList<AudiencePropertyDto>>>;

internal sealed class ListAudiencePropertiesHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : IQueryHandler<ListAudiencePropertiesQuery, Result<IReadOnlyList<AudiencePropertyDto>>>
{
    public async Task<Result<IReadOnlyList<AudiencePropertyDto>>> Handle(
        ListAudiencePropertiesQuery request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<IReadOnlyList<AudiencePropertyDto>>(gate);
        }

        return Result.Success(await LoadAsync(request.FormId, cancellationToken));
    }

    private async Task<IReadOnlyList<AudiencePropertyDto>> LoadAsync(
        long formId,
        CancellationToken cancellationToken) =>
        await db.AudienceProperties
            .Where(property => property.FormId == formId)
            .OrderBy(property => property.SortOrder)
            .ThenBy(property => property.Id)
            .Select(property => new AudiencePropertyDto(
                property.Id,
                property.FormId,
                property.VariableName,
                property.Name,
                property.DataType,
                property.SortOrder,
                property.DataListId,
                property.ChoicesJson,
                property.AllowsOther))
            .ToListAsync(cancellationToken);
}
