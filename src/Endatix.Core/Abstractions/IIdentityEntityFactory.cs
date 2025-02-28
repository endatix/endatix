using Endatix.Core.Entities.Identity;

namespace Endatix.Core.Abstractions;

public interface IIdentityEntityFactory
{
    User CreateUser(long id, string userName, string email, bool isVerified);
}
