using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;
using System.Security.Cryptography;
using System.Text;

namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Represents an email verification token for a user.
/// </summary>
public class EmailVerificationToken : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// Maximum accepted raw token length for verification and invitation endpoints.
    /// </summary>
    public const int MaxRawTokenLength = 512;

    private EmailVerificationToken() { } // For EF Core

    public EmailVerificationToken(long userId, string token, DateTime expiresAt)
    {
        Guard.Against.NegativeOrZero(userId);
        Guard.Against.NullOrWhiteSpace(token);
        Guard.Against.InvalidInput(expiresAt, nameof(expiresAt), dt => dt > DateTime.UtcNow);

        UserId = userId;
        Token = HashToken(token);
        RawToken = token;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// The ID of the user this token belongs to.
    /// </summary>
    public long UserId { get; private set; }

    /// <summary>
    /// The verification token hash.
    /// </summary>
    public string Token { get; private set; } = null!;

    /// <summary>
    /// The raw token value. This is available only immediately after token creation and is never persisted.
    /// </summary>
    public string? RawToken { get; private set; }

    /// <summary>
    /// When the token expires.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Whether the token has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Whether the token has been used.
    /// </summary>
    public bool IsUsed { get; private set; }

    /// <summary>
    /// Marks the token as used.
    /// </summary>
    public void MarkAsUsed()
    {
        IsUsed = true;
    }

    public static string HashToken(string token)
    {
        Guard.Against.NullOrWhiteSpace(token, nameof(token));

        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }
}