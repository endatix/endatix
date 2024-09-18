using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

public class User : BaseEntity, IAggregateRoot
{

    public virtual string? UserName { get; private set; }

    public virtual string? Email { get; private set; }
}
