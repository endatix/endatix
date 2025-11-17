using System.Security.Cryptography;
using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

public class Token
{
    private const int TOKEN_SIZE_BYTES = 32; // 256 bits

    public string Value { get; private set; } = null!;
    public DateTime? ExpiresAt { get; private set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    private Token() { }

    public Token(int? expiryInHours)
    {
        if (expiryInHours.HasValue)
        {
            Guard.Against.NegativeOrZero(expiryInHours.Value, nameof(expiryInHours));
        }

        Value = GenerateToken();
        ExpiresAt = expiryInHours.HasValue ? DateTime.UtcNow.AddHours(expiryInHours.Value) : null;
    }

    public void Extend(int? expiryInHours)
    {
        if (expiryInHours.HasValue)
        {
            Guard.Against.NegativeOrZero(expiryInHours.Value, nameof(expiryInHours));
            ExpiresAt = DateTime.UtcNow.AddHours(expiryInHours.Value);
        }
        else
        {
            ExpiresAt = null; // Remove expiration
        }
    }

    private string GenerateToken()
    {
        var tokenBytes = new byte[TOKEN_SIZE_BYTES];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes);
    }
}
