using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class RequiredActionEvaluation(IEnumerable<IRequiredActionProvider> providers)
{
    internal async Task<AuthorizationData> ApplyAsync(AuthorizationData data, DateTime now, CancellationToken cancellationToken)
    {
        if (providers is IReadOnlyCollection<IRequiredActionProvider> { Count: 0 })
        {
            return data;
        }

        var subject = new RequiredActionSubject(data.UserId, data.TenantId, data.Roles);
        List<RequiredAction> actions = [];
        foreach (var provider in providers)
        {
            var pending = await provider.GetPendingAsync(subject, cancellationToken);
            actions.AddRange(pending.Select(action => ForUser(action, data.Roles, now)));
        }
        return data with { RequiredActions = actions.OrderBy(a => a.Key, StringComparer.Ordinal).ToArray() };
    }

    private static RequiredAction ForUser(RequiredAction action, HashSet<string> roles, DateTime now)
    {
        var resolverRoles = action.ResolverRoles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var canResolve = resolverRoles.Length == 0 || roles.Overlaps(resolverRoles);
        var blocking = action.Severity == RequiredActionSeverity.Blocking || action.BlockingFrom <= now;
        return action with
        {
            ResolverRoles = resolverRoles,
            CanResolve = canResolve,
            Severity = blocking && (canResolve || action.BlockNonResolvers)
                ? RequiredActionSeverity.Blocking : RequiredActionSeverity.Notice
        };
    }

    internal static DateTime NextTransition(AuthorizationData data, DateTime now) =>
        data.RequiredActions.Where(a => a.Severity == RequiredActionSeverity.Notice && (a.CanResolve || a.BlockNonResolvers))
            .Select(a => a.BlockingFrom?.UtcDateTime).Where(t => t > now).Min() ?? default;
}
