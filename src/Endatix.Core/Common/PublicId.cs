using System.Buffers;

namespace Endatix.Core.Common;

/// <summary>
/// Opaque public identifiers for unauthenticated routing (tenant URLs now; forms later).
/// Not derived from names. Uniqueness is enforced by each aggregate's unique index, not here.
/// </summary>
public static class PublicId
{
    /// <summary>
    /// Nanoid / YouTube URL-safe alphabet (64 symbols).
    /// </summary>
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>
    /// Length of tenant public ids stored on <c>Tenant.Slug</c>.
    /// Short enough for URLs; collision risk is negligible under 10K tenants with a 64-symbol alphabet.
    /// </summary>
    public const int TenantLength = 8;

    /// <summary>
    /// Draws to attempt when a generated id collides with an existing unique index.
    /// </summary>
    public const int CollisionRetries = 3;

    /// <summary>
    /// Caps rejection sampling when preferring letter-heavy ids (more letters than digits).
    /// </summary>
    public const int LetterHeavyDrawRetries = 32;

    private static readonly SearchValues<char> AlphabetChars = SearchValues.Create(Alphabet);

    /// <summary>
    /// Returns true when <paramref name="value"/> is an 8-character tenant public id.
    /// </summary>
    public static bool IsValidTenantSlug(string? value) => IsValid(value, TenantLength);

    /// <summary>
    /// Returns true when <paramref name="value"/> has more Latin letters than digits.
    /// Characters outside <c>A-Za-z0-9</c> (e.g. <c>-</c>, <c>_</c>) count toward neither side.
    /// </summary>
    public static bool IsLetterHeavy(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var letters = 0;
        var digits = 0;
        foreach (var c in value)
        {
            if (char.IsAsciiLetter(c))
            {
                letters++;
            }
            else if (char.IsAsciiDigit(c))
            {
                digits++;
            }
        }

        return letters > digits;
    }

    /// <summary>
    /// Returns true when <paramref name="value"/> has the expected length and only alphabet characters.
    /// </summary>
    public static bool IsValid(string? value, int length)
    {
        if (value is null || value.Length != length)
        {
            return false;
        }

        return value.AsSpan().IndexOfAnyExcept(AlphabetChars) < 0;
    }
}
