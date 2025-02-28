using Ardalis.Specification.EntityFrameworkCore;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Infrastructure.Data;

public class EfRepository<T,TAppDbContext> : RepositoryBase<T>, IReadRepository<T>, IRepository<T>
    where T : class, IAggregateRoot
    where TAppDbContext : AppDbContext
{
    public EfRepository(TAppDbContext dbContext) : base(dbContext)
    {
    }
}

public class EfRepository<T> : EfRepository<T, AppDbContext>, IRepository<T> where T : class, IAggregateRoot
{
    public EfRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
}
