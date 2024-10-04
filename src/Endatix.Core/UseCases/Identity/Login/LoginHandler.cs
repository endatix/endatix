using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Identity.Login;

public class LoginHandler(
    IAuthService authService,
    ITokenService tokenService,
    IMediator mediator
    ) : ICommandHandler<LoginCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await authService.ValidateCredentials(request.Email, request.Password, cancellationToken);

        if (validationResult.IsInvalid())
        {
            return Result.Invalid(validationResult.ValidationErrors);
        }

        var token = tokenService.IssueToken(validationResult.Value);

        await mediator.Publish(new UserLoggedInEvent(validationResult.Value), cancellationToken);

        return Result.Success(token);
    }
}
