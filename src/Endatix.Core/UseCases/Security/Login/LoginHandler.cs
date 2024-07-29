using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Security.Login;

public class LoginHandler(IAuthService _authService, ITokenService _tokenService) : ICommandHandler<LoginCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _authService.ValidateCredentials(request.Email, request.Password, cancellationToken);

        if (validationResult.IsInvalid()){
            return Result.Invalid(validationResult.ValidationErrors);
        }

        var token = _tokenService.IssueToken(validationResult.Value);

        return Result.Success(token);
    }
}
