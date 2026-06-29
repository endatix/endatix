using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Identity.Login;

/// <summary>
/// Handles the login command by validating credentials, persisting session state, and issuing tokens.
/// </summary>
internal sealed class LoginHandler(
    IAuthService authService,
    IUserTokenService tokenService,
    ICurrentUserAuthorizationService authorizationService,
    IMediator mediator
    ) : ICommandHandler<LoginCommand, Result<AuthTokensDto>>
{
    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await authService.ValidateCredentials(
            request.Email,
            request.Password,
            cancellationToken);

        if (validationResult.IsInvalid())
        {
            return Result.Invalid(validationResult.ValidationErrors);
        }

        if (!validationResult.IsSuccess)
        {
            return Result.Error();
        }

        var user = validationResult.Value;
        var refreshToken = tokenService.IssueRefreshToken();
        var persistResult = await authService.PersistLoginSessionAsync(
            user.Id,
            refreshToken.Token,
            refreshToken.ExpireAt,
            cancellationToken);

        if (persistResult.IsInvalid())
        {
            return Result.Invalid(persistResult.ValidationErrors);
        }

        if (!persistResult.IsSuccess)
        {
            return Result.Error();
        }

        var accessToken = tokenService.IssueAccessToken(user);
        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            user.TenantId,
            cancellationToken);

        await mediator.Publish(new UserLoggedInEvent(user), cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
