using System.Security.Cryptography;
using Endatix.Core.Abstractions;
using Endatix.Core.Common;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// CSPRNG public-id generator. Uniqueness is the unique index's job; callers retry a few times.
/// </summary>
public sealed class PublicIdGenerator : IPublicIdGenerator
{
    /// <summary>
    /// Draws to attempt when a generated id collides with an existing row.
    /// </summary>
    public const int CollisionRetries = 3;

    /// <inheritdoc />
    public string Create(PublicIdKind kind)
    {
        int length = kind switch
        {
            PublicIdKind.Tenant => PublicId.TenantLength,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported public id kind.")
        };

        return RandomNumberGenerator.GetString(PublicId.Alphabet, length);
    }
}
