using Ardalis.Specification.EntityFrameworkCore;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Infrastructure.Identity.EmailVerification;

/// <summary>
/// Repository for EmailVerificationToken that uses AppIdentityDbContext.
/// </summary>
public class EmailVerificationTokenRepository : RepositoryBase<EmailVerificationToken>, IRepository<EmailVerificationToken>
{
    public EmailVerificationTokenRepository(AppIdentityDbContext dbContext) : base(dbContext)
    {
    }
} 