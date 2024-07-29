using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Security;

namespace Endatix.Core.Abstractions;

public interface IAuthService
{
    public Task<Result<UserDto>> ValidateCredentials(string email, string password, CancellationToken cancellationToken);
}
