using Ardalis.Specification;
using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification to find email verification tokens by token value.
/// </summary>
public class EmailVerificationTokenByTokenSpec : Specification<EmailVerificationToken>
{
    public EmailVerificationTokenByTokenSpec(string token)
    {
        var normalized = token.Trim();
        var tokenHash = EmailVerificationToken.HashToken(normalized);
        var presented = normalized.ToUpperInvariant();

        Query
            .AsNoTracking()
            .Where(t => t.Token == tokenHash || t.Token == presented);
    }
} 