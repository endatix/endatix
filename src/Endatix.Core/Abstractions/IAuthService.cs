using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.Abstractions;

public interface IAuthService
{
    public Task<Result<UserDto>> ValidateCredentials(string email, string password, CancellationToken cancellationToken);
}
