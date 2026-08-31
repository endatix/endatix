using System.Security.Cryptography;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// CSPRNG public-id generator. Uniqueness is the unique index's job; callers retry a few times.
/// Prefers letter-heavy ids (more letters than digits).
/// </summary>
public sealed class PublicIdGenerator : IPublicIdGenerator
{
    /// <inheritdoc />
    public string Create(PublicIdKind kind)
    {
        int length = kind switch
        {
            PublicIdKind.ShortSlug => PublicId.ShortSlugLength,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported public id kind.")
        };

        string value = RandomNumberGenerator.GetString(PublicId.Alphabet, length);
        for (var attempt = 0;
             attempt < PublicId.LetterHeavyDrawRetries && !PublicId.IsLetterHeavy(value);
             attempt++)
        {
            value = RandomNumberGenerator.GetString(PublicId.Alphabet, length);
        }

        return value;
    }
}
