﻿using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Login;

public class LoginHandler(IAuthService authService, ITokenService tokenService) : ICommandHandler<LoginCommand, Result<AuthTokensDto>>
{
    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await authService.ValidateCredentials(request.Email, request.Password, cancellationToken);

        if (validationResult.IsInvalid())
        {
            return Result.Invalid(validationResult.ValidationErrors);
        }

        if (!validationResult.IsSuccess)
        {
            return Result.Error();
        }

        var user = validationResult.Value;
        var accessToken = tokenService.IssueAccessToken(user);
        var refreshToken = tokenService.IssueRefreshToken();

        await authService.StoreRefreshToken(user.Id, refreshToken.Token, refreshToken.ExpireAt, cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
