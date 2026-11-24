using System.Security.Cryptography;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class RequiredActionEvaluation(IRequiredActionProvider[] providers, IDateTimeProvider clock)
{
    internal string ConfigurationKey { get; } = GetConfigurationKey(providers);

    private static string GetConfigurationKey(IRequiredActionProvider[] providers)
    {
        if (providers.Length == 0)
        {
            return "none";
        }

        var revisions = providers.Select(p => new
        {
            Assembly = p.GetType().Assembly.GetName().Name,
            Type = p.GetType().FullName,
            Revision = p.CacheConfigurationKey
        }).OrderBy(p => p.Assembly, StringComparer.Ordinal)
          .ThenBy(p => p.Type, StringComparer.Ordinal)
          .ThenBy(p => p.Revision, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(revisions)));
    }

    internal async Task<AuthorizationData> ApplyAsync(AuthorizationData data, CancellationToken cancellationToken)
    {
        if (providers.Length == 0)
        {
            return data;
        }

        var subject = new RequiredActionSubject(data.UserId, data.TenantId, data.Roles);
        var actions = await ReadPendingAsync(subject, cancellationToken);
        var now = clock.Now.UtcDateTime;
        return data with
        {
            RequiredActions = actions.Select(Normalize).GroupBy(action => action.Key, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal).SelectMany(CoalesceEquivalent)
                .Select(action => ForUser(action, data.Roles, now)).ToArray()
        };
    }

    private async Task<List<RequiredAction>> ReadPendingAsync(RequiredActionSubject subject, CancellationToken cancellationToken)
    {
        List<RequiredAction> actions = [];
        foreach (var provider in providers)
        {
            actions.AddRange(await provider.GetPendingAsync(subject, cancellationToken));
        }
        return actions;
    }

    private static RequiredAction Normalize(RequiredAction action) => action with
    {
        ResolverRoles = action.ResolverRoles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        BlockingFrom = action.BlockingFrom?.ToUniversalTime(),
        CanResolve = false
    };

    private static IEnumerable<RequiredAction> CoalesceEquivalent(IGrouping<string, RequiredAction> group)
    {
        var first = group.First();
        return group.All(report => report.ResolverRoles.SequenceEqual(first.ResolverRoles, StringComparer.Ordinal)
            && report with { ResolverRoles = first.ResolverRoles } == first) ? [first] : group;
    }

    private static RequiredAction ForUser(RequiredAction action, HashSet<string> roles, DateTime now)
    {
        var canResolve = action.ResolverRoles.Count == 0 || roles.Overlaps(action.ResolverRoles);
        var blocking = action.Severity == RequiredActionSeverity.Blocking || action.BlockingFrom <= now;
        return action with
        {
            CanResolve = canResolve,
            Severity = blocking && (canResolve || action.BlockNonResolvers)
                ? RequiredActionSeverity.Blocking : RequiredActionSeverity.Notice
        };
    }

    internal static DateTime NextTransition(AuthorizationData data, DateTime now) =>
        data.RequiredActions.Where(a => a.Severity == RequiredActionSeverity.Notice && (a.CanResolve || a.BlockNonResolvers))
            .Select(a => a.BlockingFrom?.UtcDateTime).Where(t => t > now).Min() ?? default;
}
