using Ardalis.Specification;

namespace Endatix.Core.Infrastructure.Domain;

public interface IRepository<T> : IRepositoryBase<T> where T : class, IAggregateRoot
{
}