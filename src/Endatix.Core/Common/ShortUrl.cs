using System.Buffers;
using Endatix.Core.Abstractions;

namespace Endatix.Core.Common;

/// <summary>
/// Compact, opaque URL segments (tenant short URLs now; forms later). Not derived from names.
/// Uniqueness is the aggregate unique index. Distinct from readable
/// <see cref="IHasUrlSlug"/> / <see cref="UrlSlugNormalizer"/> slugs.
/// </summary>
public static class ShortUrl
{
    /// <summary>
    /// Lowercase letters + digits (36). Mixed case would make uniqueness collation-dependent
    /// (PostgreSQL vs SQL Server), so stored values are already normalized.
    /// </summary>
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Length of <see cref="ShortUrlKind.Standard"/> identifiers (~2.8 × 10¹² combinations).
    /// </summary>
    public const int StandardLength = 8;

    /// <summary>
    /// Draws to attempt when a generated identifier collides with an existing unique index.
    /// </summary>
    public const int CollisionRetries = 3;

    /// <summary>
    /// Caps rejection sampling when preferring letter-heavy identifiers (more letters than digits).
    /// </summary>
    public const int LetterHeavyDrawRetries = 32;

    private static readonly SearchValues<char> AlphabetChars = SearchValues.Create(Alphabet);

    /// <summary>
    /// Returns the identifier length for <paramref name="kind"/>.
    /// </summary>
    public static int LengthOf(ShortUrlKind kind) => kind switch
    {
        ShortUrlKind.Standard => StandardLength,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported short URL kind.")
    };

    /// <summary>
    /// Strict: uppercase is rejected. Fold inbound URL segments with <see cref="Normalize"/> first.
    /// </summary>
    public static bool IsValid(string? value, ShortUrlKind kind = ShortUrlKind.Standard) =>
        IsValid(value, LengthOf(kind));

    /// <summary>
    /// True when <paramref name="value"/> has the expected length and only alphabet characters.
    /// </summary>
    public static bool IsValid(string? value, int length)
    {
        if (value is null || value.Length != length)
        {
            return false;
        }

        return value.AsSpan().IndexOfAnyExcept(AlphabetChars) < 0;
    }

    /// <summary>
    /// Trim + lowercase so a hand-typed <c>/ABC12XYZ</c> still resolves. Null/whitespace → null.
    /// </summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>
    /// True when there are more ASCII letters than digits.
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
}
