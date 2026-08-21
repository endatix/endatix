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
    /// </summary>
    public const int TenantLength = 12;

    private static readonly SearchValues<char> AlphabetChars = SearchValues.Create(Alphabet);

    /// <summary>
    /// Returns true when <paramref name="value"/> is a 12-character tenant public id.
    /// </summary>
    public static bool IsValidTenantSlug(string? value) => IsValid(value, TenantLength);

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
