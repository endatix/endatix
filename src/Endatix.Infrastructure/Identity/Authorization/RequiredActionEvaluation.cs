using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class RequiredActionEvaluation(IEnumerable<IRequiredActionProvider> providers, IDateTimeProvider clock)
{
    internal async Task<AuthorizationData> ApplyAsync(AuthorizationData data, CancellationToken cancellationToken)
    {
        if (providers is IReadOnlyCollection<IRequiredActionProvider> { Count: 0 })
        {
            return data;
        }

        var subject = new RequiredActionSubject(data.UserId, data.TenantId, data.Roles);
        var actions = await ReadPendingAsync(subject, cancellationToken);
        var now = clock.Now.UtcDateTime;
        return data with
        {
            RequiredActions = actions.OrderBy(a => a.Key, StringComparer.Ordinal)
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
