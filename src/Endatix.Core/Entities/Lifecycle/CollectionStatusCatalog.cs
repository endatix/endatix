using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Entities;

/// <summary>
/// The one owner of collection status code → <see cref="CollectionPhase"/>. Built-in codes are fixed; a host adds its
/// own codes once at startup (<c>Endatix:Submissions:CollectionStatuses</c>). The table freezes on first read, so the EF
/// value converter, the API validator and the entity all see one table.
/// </summary>
public static class CollectionStatusCatalog
{
    private static readonly FrozenDictionary<string, CollectionPhase> _builtIn =
        new Dictionary<string, CollectionPhase>(StringComparer.Ordinal)
        {
            [CollectionStatusCodes.NotStarted] = CollectionPhase.Pending,
            [CollectionStatusCodes.Viewed] = CollectionPhase.Pending,
            [CollectionStatusCodes.InProgress] = CollectionPhase.Active,
            [CollectionStatusCodes.Complete] = CollectionPhase.Completed,
            [CollectionStatusCodes.ScreenOut] = CollectionPhase.Closed,
            [CollectionStatusCodes.QuotaFull] = CollectionPhase.Closed,
            [CollectionStatusCodes.Abandoned] = CollectionPhase.Closed,
            [CollectionStatusCodes.Cancelled] = CollectionPhase.Closed,
            [CollectionStatusCodes.Expired] = CollectionPhase.Lapsed,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly Lock _configureLock = new();
    private static FrozenDictionary<string, CollectionPhase> _phases = _builtIn;
    private static FrozenDictionary<string, CollectionPhase> _custom = FrozenDictionary<string, CollectionPhase>.Empty;
    private static volatile bool _frozen;

    /// <summary>Every registered code, built-in and host, with its phase.</summary>
    public static IReadOnlyDictionary<string, CollectionPhase> Registered => Read();

    /// <summary>
    /// Adds host codes with their phases. Codes are normalized like <see cref="CollectionStatus.FromCode"/>. Calling it
    /// again with the same codes, or with none, changes nothing.
    /// </summary>
    /// <exception cref="ArgumentException">A code is empty or longer than 32, or gives a built-in code another phase.</exception>
    /// <exception cref="InvalidOperationException">The table was read already and this adds different codes.</exception>
    public static void Configure(IReadOnlyDictionary<string, CollectionPhase> customCodes)
    {
        ArgumentNullException.ThrowIfNull(customCodes);
        var requested = Normalize(customCodes);
        lock (_configureLock)
        {
            if (requested.Count == 0 || SameCodes(requested, _custom))
            {
                return;
            }

            if (_frozen)
            {
                throw new InvalidOperationException(
                    "Collection status codes were already read; register host codes once, at startup.");
            }

            _custom = requested.ToFrozenDictionary(StringComparer.Ordinal);
            _phases = _builtIn.Concat(requested).ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// A registered code, built-in or host, as a <see cref="CollectionStatus"/>. Unknown, empty or too-long codes
    /// answer false: the API answers them with a 400.
    /// </summary>
    public static bool TryGet(string? code, [NotNullWhen(true)] out CollectionStatus? status)
    {
        status = null;
        if (!TryNormalize(code, out var normalized) || !Read().ContainsKey(normalized))
        {
            return false;
        }

        status = CollectionStatus.FromCode(normalized);
        return true;
    }

    /// <summary>
    /// The phase of a code. A stored code that is not registered reads as <see cref="CollectionPhase.Closed"/>: it
    /// loads, can't resume, and raises nothing until a registered move.
    /// </summary>
    internal static CollectionPhase PhaseOf(string code) =>
        Read().TryGetValue(code, out var phase) ? phase : CollectionPhase.Closed;

    private static FrozenDictionary<string, CollectionPhase> Read()
    {
        _frozen = true;
        return _phases;
    }

    private static Dictionary<string, CollectionPhase> Normalize(IReadOnlyDictionary<string, CollectionPhase> customCodes)
    {
        Dictionary<string, CollectionPhase> normalized = new(StringComparer.Ordinal);
        foreach (var (code, phase) in customCodes)
        {
            if (!TryNormalize(code, out var key))
            {
                throw new ArgumentException(
                    $"Collection status code '{code}' must be 1 to {CollectionStatus.CODE_MAX_LENGTH} characters.",
                    nameof(customCodes));
            }

            if (!Enum.IsDefined(phase))
            {
                throw new ArgumentException($"Collection status code '{key}' has no valid phase.", nameof(customCodes));
            }

            if (_builtIn.TryGetValue(key, out var builtInPhase))
            {
                if (builtInPhase != phase)
                {
                    throw new ArgumentException(
                        $"Built-in collection status '{key}' is {builtInPhase.ToWireName()}; it can't be registered as {phase.ToWireName()}.",
                        nameof(customCodes));
                }

                continue;
            }

            normalized[key] = phase;
        }

        return normalized;
    }

    private static bool TryNormalize(string? code, [NotNullWhen(true)] out string? normalized)
    {
        normalized = code?.Trim().ToLowerInvariant();
        return normalized is { Length: > 0 and <= CollectionStatus.CODE_MAX_LENGTH };
    }

    private static bool SameCodes(Dictionary<string, CollectionPhase> requested, FrozenDictionary<string, CollectionPhase> current) =>
        requested.Count == current.Count
        && requested.All(pair => current.TryGetValue(pair.Key, out var phase) && phase == pair.Value);
}
