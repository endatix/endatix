using System.Security.Cryptography;
using Ardalis.GuardClauses;

namespace Endatix.Core.Entities;

public class Token
{
    private const int TOKEN_SIZE_BYTES = 32; // 256 bits

    public string Value { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public Token() { }

    public Token(int expiryInHours)
    {
        Guard.Against.NegativeOrZero(expiryInHours);

        Value = GenerateToken();
        ExpiresAt = DateTime.UtcNow.AddHours(expiryInHours);
    }

    public void Extend(int expiryInHours)
    {
        Guard.Against.NegativeOrZero(expiryInHours);

        ExpiresAt = DateTime.UtcNow.AddHours(expiryInHours);
    }

    private string GenerateToken()
    {
        var tokenBytes = new byte[TOKEN_SIZE_BYTES];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes);
    }
}
