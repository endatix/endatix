using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Endatix.Modules.Personalization.Shared;
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
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        Result formResult = await FormAudienceGuard.EnsureFormExistsAsync(
            forms, request.FormId, cancellationToken);
        if (!formResult.IsSuccess)
        {
            return Result.NotFound(formResult.Errors.ToArray());
        }

        List<AudiencePropertyDto> properties = await db.AudienceProperties
            .Where(property => property.FormId == request.FormId)
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

        return Result.Success<IReadOnlyList<AudiencePropertyDto>>(properties);
    }
}
