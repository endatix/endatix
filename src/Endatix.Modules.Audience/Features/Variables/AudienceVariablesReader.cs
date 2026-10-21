using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Features.Properties;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// The only database read behind a form's variables, in three statements: the Properties in tab order, the Test
/// participants, and their live cells. Removed participants and deleted Properties are left out by the query filters.
/// The tenant is stated as well as filtered, so SQL Server seeks the tenant-first indexes.
/// </summary>
internal sealed class AudienceVariablesReader(IAudienceDbContext db)
{
    public async Task<FormAudience> ReadAsync(FormKey key, CancellationToken cancellationToken)
    {
        List<Property> properties = await db.Properties.AsNoTracking()
            .Where(property => property.TenantId == key.TenantId && property.FormId == key.FormId)
            .InTabOrder()
            .ToListAsync(cancellationToken);
        return properties.Count == 0
            ? new FormAudience(properties, [])
            : new FormAudience(properties, await TestParticipantsAsync(key, cancellationToken));
    }

    private async Task<List<PresetSource>> TestParticipantsAsync(FormKey key, CancellationToken cancellationToken)
    {
        var testParticipants = await db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == key.TenantId
                && membership.FormId == key.FormId
                && membership.IsTest)
            .Join(
                db.Members.Where(member => member.TenantId == key.TenantId),
                membership => membership.MemberId,
                member => member.Id,
                (membership, member) => new { MembershipId = membership.Id, member.Identifier })
            .ToListAsync(cancellationToken);
        ILookup<long, PropertyValue> cells = await CellsAsync(
            key.TenantId, testParticipants.ConvertAll(participant => participant.MembershipId), cancellationToken);
        return testParticipants.ConvertAll(participant => new PresetSource(
            participant.Identifier,
            cells[participant.MembershipId].ToDictionary(cell => cell.PropertyId, cell => cell.Value)));
    }

    private async Task<ILookup<long, PropertyValue>> CellsAsync(
        long tenantId,
        List<long> membershipIds,
        CancellationToken cancellationToken)
    {
        if (membershipIds.Count == 0)
        {
            return Array.Empty<PropertyValue>().ToLookup(cell => cell.MembershipId);
        }

        List<PropertyValue> cells = await PropertyValuesWriter.ActiveCells(db, tenantId, membershipIds)
            .ToListAsync(cancellationToken);
        return cells.ToLookup(cell => cell.MembershipId);
    }
}

/// <summary>One form of one tenant.</summary>
internal readonly record struct FormKey(long TenantId, long FormId);
