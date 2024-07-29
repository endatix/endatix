using Ardalis.Specification.EntityFrameworkCore;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Infrastructure.Data;

public class EfRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T> where T : class, IAggregateRoot
{
    public EfRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
}