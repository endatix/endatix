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
        Query
            .AsNoTracking()
            .Where(t => t.Token == token);
    }
} 