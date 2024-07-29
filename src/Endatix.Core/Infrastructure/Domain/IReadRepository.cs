using Ardalis.Specification;

namespace Endatix.Core.Infrastructure.Domain;

public interface IReadRepository<T> : IReadRepositoryBase<T> where T : class, IAggregateRoot
{
}