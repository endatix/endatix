using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Security;

namespace Endatix.Infrastructure.Auth;

public class ConfigBasedAuthService : IAuthService
{
    private readonly SecuritySettings _securitySettings;

    public ConfigBasedAuthService(IOptions<SecuritySettings> securitySettings)
    {
        this._securitySettings = securitySettings.Value;
    }

    public async Task<Result<UserDto>> ValidateCredentials(string email, string password, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(email, nameof(email));
        Guard.Against.NullOrEmpty(password, nameof(password));

        var devUsers = _securitySettings.DevUsers;

        var matchingUser = devUsers.Where(u => u.Email.Equals(email) && u.Password.Equals(password))
            .FirstOrDefault();

        if (matchingUser != null)
        {
            var validUser = new UserDto(email, matchingUser.Roles, "System user extracted from DevUsers config section. Note: use on Development only!");
            return Result.Success<UserDto>(validUser);
        }

        return Result.Invalid(new ValidationError("Invalid credentials"));
    }
}
