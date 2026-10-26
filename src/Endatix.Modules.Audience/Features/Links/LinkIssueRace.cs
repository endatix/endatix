using Endatix.Core.Abstractions.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// Two issuers can both pass the existence check. The membership index refuses the loser; a second pass returns only
/// the links that were not saved.
/// </summary>
internal static class LinkIssueRace
{
    public static async Task<T> RetryOnceAsync<T>(
        IAudienceDbContext db,
        IUniqueConstraintViolationChecker violations,
        Func<Task<T>> issue)
    {
        try
        {
            return await issue();
        }
        catch (DbUpdateException ex) when (violations.IsViolationOf(ex, AudienceLink.UniqueConstraints.OnePerMembership))
        {
            ((DbContext)db).ChangeTracker.Clear();
            return await issue();
        }
    }
}
