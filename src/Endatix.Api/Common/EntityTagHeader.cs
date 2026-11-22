using System.Globalization;

namespace Endatix.Api.Common;

/// <summary>
/// The <c>ETag</c> and <c>If-Match</c> header values for a row's <c>Version</c> (ADR 0002): one strong tag holding the
/// number, <c>"3"</c>.
/// </summary>
/// <remarks>
/// A missing <c>If-Match</c> is the endpoint's to answer (428), before it calls <see cref="TryParse"/>; a value this
/// cannot read is a 400; a version that is no longer current is a 412.
/// </remarks>
public static class EntityTagHeader
{
    /// <summary>The header value for <paramref name="version"/>: the number in quotes.</summary>
    public static string Format(long version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Reads one version from an <c>If-Match</c> value: <c>"3"</c>, or <c>3</c> without the quotes. False for a missing
    /// value, a weak tag (<c>W/"3"</c>, which If-Match cannot compare), <c>*</c>, a list, or anything not a number.
    /// </summary>
    public static bool TryParse(string? ifMatch, out long version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return false;
        }

        var value = ifMatch.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
