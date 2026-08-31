using System.Security.Cryptography;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// CSPRNG short URL generator. Uniqueness is the unique index's job; callers retry a few times.
/// Prefers letter-heavy identifiers (more letters than digits).
/// </summary>
public sealed class ShortUrlGenerator : IShortUrlGenerator
{
    /// <inheritdoc />
    public string Create(ShortUrlKind kind)
    {
        var length = ShortUrl.LengthOf(kind);

        var value = RandomNumberGenerator.GetString(ShortUrl.Alphabet, length);
        for (var attempt = 0;
             attempt < ShortUrl.LetterHeavyDrawRetries && !ShortUrl.IsLetterHeavy(value);
             attempt++)
        {
            value = RandomNumberGenerator.GetString(ShortUrl.Alphabet, length);
        }

        return value;
    }
}
