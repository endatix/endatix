using Endatix.Infrastructure.Data;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Implements the Unit of Work pattern using Entity Framework Core for the AppIdentityDbContext.
/// </summary>
public class IdentityUnitOfWork : EfUnitOfWorkBase<AppIdentityDbContext>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityUnitOfWork"/> class.
    /// </summary>
    /// <param name="context">The AppIdentityDbContext instance.</param>
    public IdentityUnitOfWork(AppIdentityDbContext context) : base(context)
    {
    }
}
