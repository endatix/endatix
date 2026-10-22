using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Features.Properties;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// The only database read behind a form's variables: the Properties in tab order, the first
/// <see cref="PresetLimit"/> Test participants by when they were added, their live cells, and the Test participant
/// count when that page is full. Removed participants and deleted Properties are left out by the query filters.
/// The tenant is stated as well as filtered, so SQL Server seeks the tenant-first indexes.
/// </summary>
internal sealed class AudienceVariablesReader(IAudienceDbContext db)
{
    /// <summary>The most presets one read returns; CSV import can add thousands of Test participants.</summary>
    public const int PresetLimit = 20;

    public async Task<FormAudience> ReadAsync(FormKey key, CancellationToken cancellationToken)
    {
        List<Property> properties = await db.Properties.AsNoTracking()
            .Where(property => property.TenantId == key.TenantId && property.FormId == key.FormId)
            .InTabOrder()
            .ToListAsync(cancellationToken);
        if (properties.Count == 0)
        {
            return new FormAudience(properties, [], 0);
        }

        IQueryable<Membership> firstTestMemberships = TestMemberships(key)
            .OrderBy(membership => membership.Id)
            .Take(PresetLimit);
        List<PresetSource> testParticipants = await TestParticipantsAsync(key, firstTestMemberships, cancellationToken);
        int total = testParticipants.Count < PresetLimit
            ? testParticipants.Count
            : await TestMemberships(key).CountAsync(cancellationToken);
        return new FormAudience(properties, testParticipants, total);
    }

    private IQueryable<Membership> TestMemberships(FormKey key) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == key.TenantId
                && membership.FormId == key.FormId
                && membership.IsTest);

    private async Task<List<PresetSource>> TestParticipantsAsync(
        FormKey key,
        IQueryable<Membership> testMemberships,
        CancellationToken cancellationToken)
    {
        var testParticipants = await testMemberships
            .Join(
                db.Members.Where(member => member.TenantId == key.TenantId),
                membership => membership.MemberId,
                member => member.Id,
                (membership, member) => new { MembershipId = membership.Id, member.Identifier })
            .ToListAsync(cancellationToken);
        if (testParticipants.Count == 0)
        {
            return [];
        }

        var cells = (await PropertyValuesWriter
                .ActiveCells(db, key.TenantId, testMemberships.Select(membership => membership.Id))
                .Select(cell => new { cell.MembershipId, cell.PropertyId, cell.Value })
                .ToListAsync(cancellationToken))
            .ToLookup(cell => cell.MembershipId);
        return testParticipants.ConvertAll(participant => new PresetSource(
            participant.Identifier,
            cells[participant.MembershipId].ToDictionary(cell => cell.PropertyId, cell => cell.Value)));
    }
}

/// <summary>One form of one tenant.</summary>
internal readonly record struct FormKey(long TenantId, long FormId);
