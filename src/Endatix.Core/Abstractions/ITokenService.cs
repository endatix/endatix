using Endatix.Core.Entities.Identity;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.Abstractions;

public interface ITokenService
{
 public TokenDto IssueToken(User forUser, string? forAudience = null);
}
