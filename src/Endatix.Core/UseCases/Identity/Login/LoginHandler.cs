using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Login;

public class LoginHandler(IAuthService authService, ITokenService tokenService) : ICommandHandler<LoginCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await authService.ValidateCredentials(request.Email, request.Password, cancellationToken);

        if (validationResult.IsInvalid()){
            return Result.Invalid(validationResult.ValidationErrors);
        }

        var token = tokenService.IssueToken(validationResult.Value);

        return Result.Success(token);
    }
}
