using Endatix.Core.UseCases.Security;

namespace Endatix.Core.Abstractions;

public interface ITokenService
{
 public TokenDto IssueToken(UserDto forUser);
}
