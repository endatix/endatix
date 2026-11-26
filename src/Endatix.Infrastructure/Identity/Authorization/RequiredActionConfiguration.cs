using System.Security.Cryptography;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using static Endatix.Infrastructure.Identity.Authorization.RequiredActionProviderCache;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>Memoizes the current host configuration without retaining scoped providers.</summary>
internal sealed class RequiredActionConfiguration
{
    private sealed record Revision(string? Assembly, string? Type, string RevisionKey, Policy Policy);
    internal sealed record Snapshot(string Key, Policy[] Policies);
    private sealed record Entry(Revision[] Revisions, Snapshot Snapshot);
    private Entry? _last;

    internal Snapshot Capture(IEnumerable<IRequiredActionProvider> providers)
    {
        var current = providers.Select(p => new Revision(p.GetType().Assembly.GetName().Name,
            p.GetType().FullName, p.CacheConfigurationKey, Policy.For(p))).ToArray();
        var prior = Volatile.Read(ref _last);
        if (prior is not null && current.AsSpan().SequenceEqual(prior.Revisions))
        {
            return prior.Snapshot;
        }

        var entry = new Entry(current, new(Hash(current), current.Select(p => p.Policy).ToArray()));
        Volatile.Write(ref _last, entry);
        return entry.Snapshot;
    }

    private static string Hash(Revision[] current)
    {
        var ordered = current.OrderBy(p => p.Assembly, StringComparer.Ordinal).ThenBy(p => p.Type, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionKey, StringComparer.Ordinal).ThenBy(p => p.Policy.FailClosed)
            .ThenBy(p => p.Policy.Timeout).ThenBy(p => p.Policy.CacheFor)
            .Select(p => new { p.Assembly, p.Type, Revision = p.RevisionKey, p.Policy });
        return "standing-v6:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ordered)));
    }
}
