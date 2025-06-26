using Ardalis.Specification;
using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification to find email verification tokens by user ID.
/// </summary>
public class EmailVerificationTokenByUserIdSpec : Specification<EmailVerificationToken>
{
    public EmailVerificationTokenByUserIdSpec(long userId)
    {
        Query
            .AsNoTracking()
            .Where(t => t.UserId == userId);
    }
} 