using Endatix.Core.Abstractions.Data;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// Two issuers can both pass the existence check. The membership index refuses the loser; the next pass returns only
/// the links that were not saved. A loss on the last pass is thrown.
/// </summary>
internal static class LinkIssueRace
{
    public static bool IsLost(IUniqueConstraintViolationChecker violations, DbUpdateException ex) =>
        violations.IsViolationOf(ex, AudienceLink.UniqueConstraints.OnePerMembership);

    public static async Task<T> RetryAsync<T>(LinkIssuePasses passes, Func<Task<T>> issue)
    {
        for (var pass = 1; pass < passes.Count; pass++)
        {
            try
            {
                return await issue();
            }
            catch (DbUpdateException ex) when (IsLost(passes.Violations, ex))
            {
                ((DbContext)passes.Db).ChangeTracker.Clear();
            }
        }

        return await issue();
    }
}

/// <summary>How many times an issue may run, and what it needs to tell a lost race and start over.</summary>
internal sealed record LinkIssuePasses(IAudienceDbContext Db, IUniqueConstraintViolationChecker Violations, int Count);
