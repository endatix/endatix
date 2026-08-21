using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RefreshToken;

/// <summary>
/// Handles the refresh token logic by validating the refresh token and issuing new access and refresh tokens.
/// Assumed sessions keep their target tenant and actor claim across refresh.
/// </summary>
public class RefreshTokenHandler(IAuthService authService, IUserTokenService tokenService) : ICommandHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var sessionResult = await tokenService.ReadAccessTokenSessionAsync(request.AccessToken, validateLifetime: false);

        if (sessionResult.IsInvalid())
        {
            return Result.Invalid(sessionResult.ValidationErrors);
        }

        if (!sessionResult.IsSuccess)
        {
            return Result.Error();
        }

        var session = sessionResult.Value;
        var refreshTokenValidationResult = await authService.ValidateRefreshToken(session.UserId, request.RefreshToken, cancellationToken);

        if (refreshTokenValidationResult.IsInvalid())
        {
            return Result.Invalid(refreshTokenValidationResult.ValidationErrors);
        }

        if (!refreshTokenValidationResult.IsSuccess)
        {
            return Result.Error();
        }

        var user = refreshTokenValidationResult.Value;
        var accessToken = session.ActorUserId is not null
            ? tokenService.IssueAccessToken(
                user,
                new AccessTokenIssueOptions(session.TenantId, session.ActorUserId))
            : tokenService.IssueAccessToken(user);
        var refreshToken = tokenService.IssueRefreshToken();

        await authService.StoreRefreshToken(user.Id, refreshToken.Token, refreshToken.ExpireAt, cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
