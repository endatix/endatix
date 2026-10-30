using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// A form's live Properties' ids, which its participants' cells are read for (<see cref="ParticipantValues"/>), and its
/// match-key Property (<see cref="MatchKeyProperty"/>), <see langword="null"/> when it has none. Readers that need the
/// Properties themselves read them whole instead (<c>AudienceVariablesReader</c>).
/// </summary>
internal sealed record FormProperties(IReadOnlyCollection<long> Ids, MatchKey? MatchKey)
{
    /// <summary>One statement: each Property is tested against <see cref="MatchKeyProperty.Of"/> by its id.</summary>
    public static async Task<FormProperties> ReadAsync(
        IAudienceDbContext db,
        MatchKeyForm form,
        CancellationToken cancellationToken)
    {
        IQueryable<long> matchKey = MatchKeyProperty.Of(db, form.TenantId, form.FormId).Select(property => property.Id);
        var properties = await db.Properties.AsNoTracking()
            .Where(property => property.TenantId == form.TenantId && property.FormId == form.FormId)
            .Select(property => new { property.Id, property.Name, IsMatchKey = matchKey.Contains(property.Id) })
            .ToListAsync(cancellationToken);
        return new FormProperties(
            properties.ConvertAll(property => property.Id),
            properties
                .Where(property => property.IsMatchKey)
                .Select(property => new MatchKey(property.Id, property.Name))
                .FirstOrDefault());
    }
}
