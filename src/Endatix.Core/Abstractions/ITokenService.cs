using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.Abstractions;

public interface ITokenService
{
 public TokenDto IssueToken(UserDto forUser);
}
