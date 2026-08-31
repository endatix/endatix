using System.Buffers;
using Endatix.Core.Abstractions;

namespace Endatix.Core.Common;

/// <summary>
/// Short URL identifiers: compact, globally unique, URL-ready segments for unauthenticated routing
/// (tenant URLs now; forms later). Optimised for length and uniqueness, not for reading.
/// Not derived from names. Uniqueness is enforced by each aggregate's unique index, not here.
/// <para>
/// Distinct from <see cref="IHasUrlSlug"/> / <see cref="UrlSlugNormalizer"/>, which produce
/// readable name-derived slugs for folders and forms. Those exist to reveal the name; these exist
/// to keep it out of the URL, so they are never normalized and never reserved-word checked.
/// </para>
/// </summary>
public static class ShortUrl
{
    /// <summary>
    /// URL-safe alphabet: lowercase letters and digits (36 symbols). Hyphens and underscores are
    /// excluded so generated ids stay visually compact (no <c>jj-8vjcr</c>-style tokens).
    /// <para>
    /// Lowercase-only is deliberate. A mixed-case alphabet behaves differently per provider:
    /// PostgreSQL compares case-sensitively while SQL Server's default collation does not, so
    /// <c>abcdefgh</c> and <c>ABCDEFGH</c> would be two tenants on one and one tenant on the other.
    /// Restricting the alphabet makes every stored value already normalized, so the unique index and
    /// every lookup mean the same thing on both providers with no collation annotation and no second
    /// normalized column.
    /// </para>
    /// </summary>
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Length of <see cref="ShortUrlKind.Standard"/> identifiers (<c>Tenant.ShortUrl</c> today).
    /// Eight symbols over a 36-symbol alphabet give ~2.8 * 10^12 combinations: brief enough for a
    /// URL, with negligible collision risk in the 10K-20K range (~4 * 10^-5 at 20K rows, and a
    /// create redraws on the unique index anyway).
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
    /// Returns true when <paramref name="value"/> is a well-formed identifier of the given kind.
    /// Strict: uppercase input is rejected rather than folded. Normalize inbound URL segments with
    /// <see cref="Normalize"/> before validating.
    /// </summary>
    public static bool IsValid(string? value, ShortUrlKind kind = ShortUrlKind.Standard) =>
        IsValid(value, LengthOf(kind));

    /// <summary>
    /// Folds an identifier taken from a URL or user input into stored form (trimmed, lowercase), so
    /// a hand-typed <c>/ABC12XYZ</c> still resolves. Returns null for null/whitespace input.
    /// Persisted values are already normalized, so lookups can compare exactly after this call.
    /// </summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>
    /// Returns true when <paramref name="value"/> has more Latin letters than digits.
    /// Characters outside <c>a-z0-9</c> count toward neither side.
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
