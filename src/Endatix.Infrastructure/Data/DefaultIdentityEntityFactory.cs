using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Data;

public class DefaultIdentityEntityFactory : IIdentityEntityFactory
{
    public User CreateUser(long id, string userName, string email, bool isVerified)
    {
        return new User(id, userName, email, isVerified);
    }
}
