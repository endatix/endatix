using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Properties;

/// <summary>
/// Lists audience properties for a form, ordered by sort order.
/// </summary>
public sealed record ListPropertiesQuery(long TenantId, long FormId)
    : IQuery<Result<IReadOnlyList<PropertyDto>>>;

internal sealed class ListPropertiesHandler(
    IAudienceDbContext db,
    IRepository<Form> forms)
    : IQueryHandler<ListPropertiesQuery, Result<IReadOnlyList<PropertyDto>>>
{
    public async Task<Result<IReadOnlyList<PropertyDto>>> Handle(
        ListPropertiesQuery request,
        CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<IReadOnlyList<PropertyDto>>();
        }

        return Result.Success(await LoadAsync(request, cancellationToken));
    }

    private async Task<IReadOnlyList<PropertyDto>> LoadAsync(
        ListPropertiesQuery request,
        CancellationToken cancellationToken)
    {
        List<Property> properties = await db.Properties
            .AsNoTracking()
            .Where(property => property.TenantId == request.TenantId && property.FormId == request.FormId)
            .OrderBy(property => property.SortOrder)
            .ThenBy(property => property.Id)
            .ToListAsync(cancellationToken);
        return properties.Select(PropertyDto.From).ToList();
    }
}
