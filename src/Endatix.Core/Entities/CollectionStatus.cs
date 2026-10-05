using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

/// <summary>Built-in wire codes for <see cref="CollectionStatus"/>. Unknown codes stay legal.</summary>
public static class CollectionStatusCodes
{
    public const string InProgress = "in_progress";
    public const string Complete = "complete";
    public const string ScreenOut = "screen_out";
    public const string QuotaFull = "quota_full";
    public const string Expired = "expired";
    public const string Abandoned = "abandoned";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Fielding disposition. Built-in codes are catalog statics. Any other non-empty code constructs.
/// </summary>
public sealed record CollectionStatus
{
    public const int CODE_MAX_LENGTH = 32;

    private static readonly Dictionary<string, CollectionStatus> BuiltIn = new(StringComparer.Ordinal)
    {
        [CollectionStatusCodes.InProgress] = new("In progress", CollectionStatusCodes.InProgress),
        [CollectionStatusCodes.Complete] = new("Complete", CollectionStatusCodes.Complete),
        [CollectionStatusCodes.ScreenOut] = new("Screened out", CollectionStatusCodes.ScreenOut),
        [CollectionStatusCodes.QuotaFull] = new("Quota full", CollectionStatusCodes.QuotaFull),
        [CollectionStatusCodes.Expired] = new("Expired", CollectionStatusCodes.Expired),
        [CollectionStatusCodes.Abandoned] = new("Abandoned", CollectionStatusCodes.Abandoned),
        [CollectionStatusCodes.Cancelled] = new("Cancelled", CollectionStatusCodes.Cancelled),
    };

    public static readonly CollectionStatus InProgress = BuiltIn[CollectionStatusCodes.InProgress];
    public static readonly CollectionStatus Complete = BuiltIn[CollectionStatusCodes.Complete];
    public static readonly CollectionStatus ScreenOut = BuiltIn[CollectionStatusCodes.ScreenOut];
    public static readonly CollectionStatus QuotaFull = BuiltIn[CollectionStatusCodes.QuotaFull];
    public static readonly CollectionStatus Expired = BuiltIn[CollectionStatusCodes.Expired];
    public static readonly CollectionStatus Abandoned = BuiltIn[CollectionStatusCodes.Abandoned];
    public static readonly CollectionStatus Cancelled = BuiltIn[CollectionStatusCodes.Cancelled];

    private CollectionStatus()
    {
        Code = CollectionStatusCodes.InProgress;
    }

    private CollectionStatus(string name, string code)
    {
        Code = Normalize(code);
        Name = name;
    }

    public string Code { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CollectionStatus CreateInstance() => this with { };

    /// <summary>
    /// Built-in codes return the catalog instance. Any other code is accepted.
    /// </summary>
    public static CollectionStatus FromCode(string code)
    {
        var normalized = Normalize(code);
        return BuiltIn.TryGetValue(normalized, out var catalog)
            ? catalog.CreateInstance()
            : new CollectionStatus(normalized, normalized);
    }

    private static string Normalize(string code)
    {
        Guard.Against.NullOrWhiteSpace(code);
        var normalized = code.Trim().ToLowerInvariant();
        Guard.Against.OutOfRange(normalized.Length, nameof(code), 1, CODE_MAX_LENGTH);
        return normalized;
    }
}
