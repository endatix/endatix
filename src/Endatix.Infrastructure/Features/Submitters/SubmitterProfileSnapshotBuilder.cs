using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Builds a profile snapshot from a dictionary of claims.
/// </summary>
internal sealed class SubmitterProfileSnapshotBuilder(IOptions<SubmitterOptions> options)
{
    public string? Build(IReadOnlyDictionary<string, string>? profile)
    {
        if (profile is null || profile.Count is 0 || options.Value.ProfileSnapshotFields.Count is 0)
        {
            return null;
        }

        Dictionary<string, string> snapshot = new(StringComparer.Ordinal);
        foreach (var field in options.Value.ProfileSnapshotFields)
        {
            if (profile.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                snapshot[field] = value;
            }
        }

        return snapshot.Count is 0
            ? null
            : JsonSerializer.Serialize(snapshot);
    }
}
