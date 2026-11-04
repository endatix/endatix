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

            EnsureNotRead();
            _custom = requested.ToFrozenDictionary(StringComparer.Ordinal);
            _phases = _builtIn.Concat(requested).ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    private static void EnsureNotRead()
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                "Collection status codes were already read; register host codes once, at startup.");
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

    // Host codes only: a built-in repeated with its own phase is dropped.
    private static Dictionary<string, CollectionPhase> Normalize(IReadOnlyDictionary<string, CollectionPhase> customCodes) =>
        customCodes
            .Select(pair => (Code: NormalizeCode(pair.Key), Phase: EnsurePhase(pair.Key, pair.Value)))
            .Where(entry => !_builtIn.ContainsKey(entry.Code))
            .ToDictionary(entry => entry.Code, entry => entry.Phase, StringComparer.Ordinal);

    private static string NormalizeCode(string code) =>
        TryNormalize(code, out var normalized)
            ? normalized
            : throw new ArgumentException(
                $"Collection status code '{code}' must be 1 to {CollectionStatus.CODE_MAX_LENGTH} characters.", nameof(code));

    private static CollectionPhase EnsurePhase(string code, CollectionPhase phase)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentException($"Collection status code '{code}' has no valid phase.", nameof(phase));
        }

        if (_builtIn.TryGetValue(NormalizeCode(code), out var builtInPhase) && builtInPhase != phase)
        {
            throw new ArgumentException(
                $"Built-in collection status '{code}' is {builtInPhase.ToWireName()}; it can't be registered as {phase.ToWireName()}.",
                nameof(phase));
        }

        return phase;
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
