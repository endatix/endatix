using System.Text;
using System.Text.RegularExpressions;

namespace Endatix.Core.Common;

/// <summary>
/// Normalizes user-facing names into URL path segments (lowercase, hyphen-separated).
/// Policy: path-segment slugs for public routes; see product docs before changing rules (Hub must stay in sync).
/// Hub mirrors: hub/lib/url/folder-slug.ts
/// </summary>
public static class UrlSlugNormalizer
{
    /// <summary>
    /// The maximum length of a slug.
    /// </summary>
    public const int MAX_SLUG_LENGTH = 128;
    private const char HYPHEN_CHAR = '-';

    private static readonly Regex _collapseHyphensRegex = new(@"-{2,}", RegexOptions.Compiled);

    private static readonly Regex _invalidCharsRegex = new(@"[^a-z0-9\-]", RegexOptions.Compiled);

    /// <summary>
    /// The reserved slugs to avoid onfusion for users/collisions with existing routes.
    /// </summary>
    public static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "templates", "new", "api", "folders", "by-slug", "design", "analytics", "submissions", "share", "embed", "preview", "login", "signup", "logout", "register", "forgot-password", "reset-password", "verify-email", "email-verification", "email-confirmation"
    };

    /// <summary>
    /// Produces a slug from a display name (e.g. folder title).
    /// </summary>
    /// <param name="name">The display name to convert to a slug.</param>
    /// <returns>The slug.</returns>
    public static string FromDisplayName(string name)
    {
        return Normalize(name);
    }

    /// <summary>
    /// Normalizes an explicit slug input.
    /// </summary>
    /// <param name="raw">The raw string to normalize.</param>
    /// <returns>The normalized string.</returns>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (IsValidUrlSlugChar(ch))
            {
                builder.Append(ch);
            }
            else if (IsLogicalSeparator(ch))
            {
                builder.Append(HYPHEN_CHAR);
            }
        }

        var normalized = _collapseHyphensRegex.Replace(builder.ToString(), $"{HYPHEN_CHAR}").Trim(HYPHEN_CHAR);
        if (normalized.Length > MAX_SLUG_LENGTH)
        {
            normalized = normalized[..MAX_SLUG_LENGTH].Trim(HYPHEN_CHAR);
        }

        return normalized;
    }

    /// <summary>
    /// Checks if a slug is reserved according to the rules.
    /// </summary>
    /// <param name="slug">The slug to check.</param>
    /// <returns>True if the slug is reserved, false otherwise.</returns>
    public static bool IsReserved(string slug) => ReservedSlugs.Contains(slug);

    /// <summary>
    /// Checks if a slug is valid according to the rules.
    /// </summary>
    /// <param name="slug">The slug to check.</param>
    /// <returns>True if the slug is valid, false otherwise.</returns>
    public static bool IsValidFormat(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > MAX_SLUG_LENGTH)
        {
            return false;
        }

        if (slug.StartsWith(HYPHEN_CHAR) || slug.EndsWith(HYPHEN_CHAR))
        {
            return false;
        }

        return !_invalidCharsRegex.IsMatch(slug) && slug.All(IsValidUrlSlugChar);
    }


    /// <summary>
    /// Checks if a character is a valid URL slug character.
    /// </summary>
    /// <param name="ch">The character to check.</param>
    /// <returns>True if the character is a valid URL slug character, false otherwise.</returns>
    private static bool IsValidUrlSlugChar(char ch) => char.IsAsciiLetterOrDigit(ch) || ch == HYPHEN_CHAR;

    /// <summary>
    /// Checks if a character is a logical separator.
    /// </summary>
    /// <param name="ch">The character to check.</param>
    /// <returns>True if the character is a logical separator, false otherwise.</returns>
    private static bool IsLogicalSeparator(char ch) => ch is ' ' or '_' or '.' or '|';
}
