using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

public interface IAuthService
{
    public Task<Result<User>> ValidateCredentials(string email, string password, CancellationToken cancellationToken);
}
