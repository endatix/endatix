using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Entities;

/// <summary>
/// The one owner of collection status code → <see cref="CollectionPhase"/>. Built-in codes are fixed; a host adds its
/// own codes once at startup (<c>Endatix:Submissions:CollectionStatuses</c>). The table freezes on first read, so the EF
/// value converter, the API validator and the entity all see one table. It is process-wide, so every host in one
/// process registers the same codes.
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
            [CollectionStatusCodes.Cancelled] = CollectionPhase.Closed,
            [CollectionStatusCodes.Expired] = CollectionPhase.Lapsed,
            [CollectionStatusCodes.Abandoned] = CollectionPhase.Lapsed,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly Lock _configureLock = new();
    private static FrozenDictionary<string, CollectionPhase> _phases = _builtIn;
    private static FrozenDictionary<string, CollectionPhase> _custom = FrozenDictionary<string, CollectionPhase>.Empty;
    private static volatile bool _frozen;
    private static readonly ConcurrentDictionary<string, byte> _unregisteredSeen = new(StringComparer.Ordinal);
    private static ILogger? _logger;

    /// <summary>
    /// Adds host codes with their phases. Codes are normalized like <see cref="CollectionStatus.FromCode"/>. Calling it
    /// again with the same codes changes nothing, and so does calling it with none while no host codes are registered.
    /// </summary>
    /// <exception cref="ArgumentException">A code is empty or longer than 32, or gives a built-in code another phase.</exception>
    /// <exception cref="InvalidOperationException">
    /// Other host codes are registered already (another host in this process), or the table was read already.
    /// </exception>
    public static void Configure(IReadOnlyDictionary<string, CollectionPhase> customCodes)
    {
        ArgumentNullException.ThrowIfNull(customCodes);
        var requested = Normalize(customCodes);
        lock (_configureLock)
        {
            if (SameCodes(requested, _custom))
            {
                return;
            }

            EnsureNotRegisteredOrRead();
            _custom = requested.ToFrozenDictionary(StringComparer.Ordinal);
            _phases = _builtIn.Concat(requested).ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    private static void EnsureNotRegisteredOrRead()
    {
        if (_custom.Count > 0)
        {
            throw new InvalidOperationException(
                "Other collection status codes are registered in this process; every host in one process registers the same codes.");
        }

        if (_frozen)
        {
            throw new InvalidOperationException(
                "Collection status codes were already read; register host codes once, at startup.");
        }
    }

    /// <summary>Where the warning for an unregistered stored code goes. Warnings already due are written now.</summary>
    public static void UseLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        foreach (var code in _unregisteredSeen.Keys)
        {
            LogUnregistered(logger, code);
        }
    }

    /// <summary>Clears host codes, the freeze and the warnings, so a test can configure the catalog again.</summary>
    internal static void ResetForTests()
    {
        lock (_configureLock)
        {
            _custom = FrozenDictionary<string, CollectionPhase>.Empty;
            _phases = _builtIn;
            _frozen = false;
            _unregisteredSeen.Clear();
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
    /// The phase of a code. A stored code that is not registered reads as <see cref="CollectionPhase.Active"/>, so its
    /// submission can still be saved and completed; the first read of each such code logs a warning.
    /// </summary>
    internal static CollectionPhase PhaseOf(string code)
    {
        if (Read().TryGetValue(code, out var phase))
        {
            return phase;
        }

        if (_unregisteredSeen.TryAdd(code, 0) && _logger is { } logger)
        {
            LogUnregistered(logger, code);
        }

        return CollectionPhase.Active;
    }

    private static void LogUnregistered(ILogger logger, string code) =>
        logger.LogWarning(
            "Collection status '{Code}' is stored but not registered in Endatix:Submissions:CollectionStatuses; it reads as active.",
            code);

    // The first read freezes the table under the lock Configure takes, so a reader never keeps a table Configure
    // replaces afterwards. Later reads take no lock and write nothing.
    private static FrozenDictionary<string, CollectionPhase> Read()
    {
        if (!_frozen)
        {
            lock (_configureLock)
            {
                _frozen = true;
            }
        }

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
