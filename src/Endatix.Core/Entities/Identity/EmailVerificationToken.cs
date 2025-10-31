using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Represents an email verification token for a user.
/// </summary>
public class EmailVerificationToken : BaseEntity, IAggregateRoot
{
    private EmailVerificationToken() { } // For EF Core

    public EmailVerificationToken(long userId, string token, DateTime expiresAt)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        Guard.Against.NullOrWhiteSpace(token, nameof(token));
        Guard.Against.InvalidInput(expiresAt, nameof(expiresAt), dt => dt > DateTime.UtcNow);

        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// The ID of the user this token belongs to.
    /// </summary>
    public long UserId { get; private set; }

    /// <summary>
    /// The verification token value.
    /// </summary>
    public string Token { get; private set; } = null!;

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
} 