using Ardalis.Specification;
using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Specifications;

/// <summary>
/// Finds an email verification token by its raw value.
/// </summary>
public class EmailVerificationTokenByTokenSpec : Specification<EmailVerificationToken>
{
    public EmailVerificationTokenByTokenSpec(string token)
    {
        // Hash only. The column stores a hash so that read access to the table is not read access
        // to the tokens; also matching the stored value verbatim would hand that property back.
        var tokenHash = EmailVerificationToken.HashToken(token.Trim());

        Query
            .AsNoTracking()
            .Where(t => t.Token == tokenHash);
    }
}
