using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// One person on a form as <see cref="PersonDto"/>: their active cells, read as the list reads them, and their Link
/// status. The list reads a whole page at once instead.
/// </summary>
internal sealed class PersonReader(IAudienceDbContext db, LinkStatuses linkStatuses)
{
    public async Task<PersonDto> ReadAsync(Membership membership, string identifier, CancellationToken cancellationToken)
    {
        Dictionary<long, string> values = await PropertyValuesWriter
            .ActiveCells(db, membership.TenantId, [membership.Id])
            .ToDictionaryAsync(value => value.PropertyId, value => value.Value, cancellationToken);
        IReadOnlyDictionary<long, LinkStatusDto> links = await linkStatuses.ForAsync([membership.Id], cancellationToken);
        return new PersonDto(
            membership.Id, membership.MemberId, identifier, values, membership.IsTest, links[membership.Id]);
    }
}
