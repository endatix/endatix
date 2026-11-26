using System.Security.Cryptography;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using static Endatix.Infrastructure.Identity.Authorization.RequiredActionProviderCache;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class RequiredActionEvaluation(IEnumerable<IRequiredActionProvider> registrations, RequiredActionProviderCache reports)
{
    private readonly IRequiredActionProvider[] _providers = registrations.ToArray();
    internal bool HasProviders => _providers.Length > 0;
    internal string ConfigurationKey { get; } = GetConfigurationKey(registrations.ToArray());

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
            Revision = p.CacheConfigurationKey,
            Policy = Policy.For(p)
        }).OrderBy(p => p.Assembly, StringComparer.Ordinal)
          .ThenBy(p => p.Type, StringComparer.Ordinal)
          .ThenBy(p => p.Revision, StringComparer.Ordinal)
          .ThenBy(p => p.Policy.FailClosed).ThenBy(p => p.Policy.Timeout).ThenBy(p => p.Policy.CacheFor);
        // Do not reuse snapshots built without the provider's freshness or failure policy.
        return "standing-v5:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(revisions)));
    }

    internal async Task<Func<DateTime, AuthorizationData>> EvaluateAsync(AuthorizationData data, AuthorizationCache.CacheEntry entry, CancellationToken ct)
    {
        if (data.UnverifiedActor)
        {
            return _ => data with { RequiredActionsUnavailable = true, RequiredActions = [] };
        }
        if (_providers.Length == 0)
        {
            return _ => data;
        }

        var subject = new RequiredActionSubject(data.UserId, data.TenantId, data.Roles) { VerifiedAssumption = data.VerifiedAssumption };
        var epoch = await reports.SubjectEpochAsync(subject, entry, ct);
        var queries = _providers.Select((p, i) => new Query(epoch, i, subject, Policy.For(p))).ToArray();
        var results = await Task.WhenAll(queries.Select(q => reports.ReadAsync(q, ct)));
        var evaluated = new ProviderReports(queries, results);
        return now => Combine(data, evaluated, now);
    }

    private sealed record ProviderReports(Query[] Queries, Report[] Results);

    private AuthorizationData Combine(AuthorizationData data, ProviderReports providers, DateTime now)
    {
        var (queries, results) = providers;
        if (queries.Any(q => results[q.Index].Unavailable || (q.Policy.FailClosed && results[q.Index].Until <= now)))
        {
            return BoundExpiry(data with { RequiredActionsUnavailable = true, RequiredActions = [] }, results.Min(r => r.Until));
        }
        var projected = Project(data, results.Where(r => r.UsableUntil > now).SelectMany(r => r.Actions), now);
        if (projected.RequiredActionsUnavailable)
        {
            foreach (var q in queries)
            {
                reports.ForgetContradiction(q, results[q.Index]);
            }
        }
        return BoundExpiry(projected, results.Min(r => r.ProjectionExpiry(now)));
    }

    private static AuthorizationData BoundExpiry(AuthorizationData data, DateTime until)
    {
        return data with { ExpiresAt = data.ExpiresAt != default && data.ExpiresAt < until ? data.ExpiresAt : until };
    }

    private static AuthorizationData Project(AuthorizationData data, IEnumerable<RequiredAction> actions, DateTime now)
    {
        var groups = actions.Select(Normalize).GroupBy(action => action.Key, StringComparer.Ordinal);
        List<RequiredAction> projected = [];
        foreach (var group in groups.OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var report = group.First();
            if (!group.All(other => Equivalent(report, other)))
            {
                return data with { RequiredActionsUnavailable = true, RequiredActions = [] };
            }
            projected.Add(ForUser(report, data.Roles, now));
        }
        return data with { RequiredActionsUnavailable = false, RequiredActions = projected };
    }

    private static RequiredAction Normalize(RequiredAction action) => action with
    {
        ResolverRoles = action.ResolverRoles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        BlockingFrom = action.BlockingFrom?.ToUniversalTime(),
        CanResolve = false
    };

    private static bool Equivalent(RequiredAction first, RequiredAction other) =>
        other.ResolverRoles.SequenceEqual(first.ResolverRoles, StringComparer.Ordinal)
        && other with { ResolverRoles = first.ResolverRoles } == first;

    private static RequiredAction ForUser(RequiredAction action, HashSet<string> roles, DateTime now)
    {
        var canResolve = !action.PreventResolution && (action.ResolverRoles.Count == 0 || roles.Overlaps(action.ResolverRoles));
        var blocking = action.Severity == RequiredActionSeverity.Blocking || action.BlockingFrom <= now;
        return action with
        {
            CanResolve = canResolve,
            PreventResolution = false,
            Severity = blocking && (canResolve || action.BlockNonResolvers)
                ? RequiredActionSeverity.Blocking : RequiredActionSeverity.Notice
        };
    }

    internal static DateTime NextTransition(AuthorizationData data, DateTime now) =>
        data.RequiredActions.Where(a => a.Severity == RequiredActionSeverity.Notice && (a.CanResolve || a.BlockNonResolvers))
            .Select(a => a.BlockingFrom?.UtcDateTime).Where(t => t > now).Min() ?? default;
}
