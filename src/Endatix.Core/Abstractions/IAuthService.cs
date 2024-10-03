using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

public interface IAuthService
{
    public Task<Result<User>> ValidateCredentials(string email, string password, CancellationToken cancellationToken);

    public Task<Result<User>> ValidateRefreshToken(long userId, string refreshToken, CancellationToken cancellationToken);

    public Task<Result> StoreRefreshToken(long userId, string token, DateTime expireAt, CancellationToken cancellationToken);
}
